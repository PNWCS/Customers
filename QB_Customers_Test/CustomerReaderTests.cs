using System.Diagnostics;
using Serilog;
using QB_Customers_Lib;  
using QBFC16Lib;         // For QuickBooks session and API interaction.
using static QB_Customers_Test.CommonMethods;
using QB_Customers_Test;

namespace QB_Customers_Test
{
    [Collection("Sequential Tests")]
    public class CustomerReaderTests
    {
        [Fact]
        public void AddAndReadMultipleCustomers_FromQuickBooks_And_Verify_Logs()
        {
            const int CUSTOMER_COUNT = 5;
            const int STARTING_COMPANY_ID = 100;
            var customersToAdd = new List<Customer>();

            // 1) Ensure Serilog has released file access before deleting old logs.
            EnsureLogFileClosed();
            DeleteOldLogFiles();
            ResetLogger();

            // 2) Build a list of random Customer objects with a name and fax (using fax for company id).
            for (int i = 0; i < CUSTOMER_COUNT; i++)
            {
                string randomName = "TestCustomer_" + Guid.NewGuid().ToString("N").Substring(0, 8);
                int companyID = STARTING_COMPANY_ID + i;
                string fax = companyID.ToString();
                customersToAdd.Add(new Customer(randomName, fax));
            }

            // 3) Add customers directly to QuickBooks.
            using (var qbSession = new QuickBooksSession(AppConfig.QB_APP_NAME))
            {
                foreach (var customer in customersToAdd)
                {
                    string qbID = AddCustomer(qbSession, customer.Name, customer.Fax);
                    customer.QB_ID = qbID; // Store the returned QB ListID.
                }
            }

            // 4) Query QuickBooks to retrieve all customers.
            var allQBCustomers = CustomerReader.QueryAllCustomers();

            // 5) Verify that all added customers are present in QuickBooks.
            foreach (var customer in customersToAdd)
            {
                var matchingCustomer = allQBCustomers.FirstOrDefault(c => c.QB_ID == customer.QB_ID);
                Assert.NotNull(matchingCustomer);
                Assert.Equal(customer.Name, matchingCustomer.Name);
                Assert.Equal(customer.Fax, matchingCustomer.Fax);
            }

            // 6) Cleanup: Delete the added customers.
            using (var qbSession = new QuickBooksSession(AppConfig.QB_APP_NAME))
            {
                foreach (var customer in customersToAdd.Where(c => !string.IsNullOrEmpty(c.QB_ID)))
                {
                    DeleteCustomer(qbSession, customer.QB_ID);
                }
            }

            // 7) Ensure logs are fully flushed before accessing them.
            EnsureLogFileClosed();

            // 8) Verify that a new log file exists.
            string logFile = GetLatestLogFile();
            EnsureLogFileExists(logFile);

            // 9) Read the log file content.
            string logContents = File.ReadAllText(logFile);

            // 10) Assert expected log messages exist.
            Assert.Contains("CustomerReader Initialized", logContents);
            Assert.Contains("CustomerReader Completed", logContents);

            // 11) Verify that each retrieved customer was logged properly.
            foreach (var customer in customersToAdd)
            {
                string expectedLogMessage = $"Successfully retrieved {customer.Name} from QB";
                Assert.Contains(expectedLogMessage, logContents);
            }
        }

        private string AddCustomer(QuickBooksSession qbSession, string name, string fax)
        {
            IMsgSetRequest requestMsgSet = qbSession.CreateRequestSet();
            ICustomerAdd customerAddRq = requestMsgSet.AppendCustomerAddRq();
            customerAddRq.Name.SetValue(name);
            customerAddRq.Fax.SetValue(fax);
            // Additional customer fields can be set here as needed.

            IMsgSetResponse responseMsgSet = qbSession.SendRequest(requestMsgSet);
            return ExtractListIDFromResponse(responseMsgSet);
        }

        private string ExtractListIDFromResponse(IMsgSetResponse responseMsgSet)
        {
            IResponseList responseList = responseMsgSet.ResponseList;
            if (responseList == null || responseList.Count == 0)
                throw new Exception("No response from CustomerAddRq.");

            IResponse response = responseList.GetAt(0);
            if (response.StatusCode != 0)
                throw new Exception($"CustomerAdd failed: {response.StatusMessage}");

            ICustomerRet? customerRet = response.Detail as ICustomerRet;
            if (customerRet == null)
                throw new Exception("No ICustomerRet returned after adding Customer.");

            return customerRet.ListID?.GetValue()
                ?? throw new Exception("ListID is missing in QuickBooks response.");
        }

        private void DeleteCustomer(QuickBooksSession qbSession, string listID)
        {
            IMsgSetRequest requestMsgSet = qbSession.CreateRequestSet();
            IListDel listDelRq = requestMsgSet.AppendListDelRq();
            listDelRq.ListDelType.SetValue(ENListDelType.ldtCustomer);
            listDelRq.ListID.SetValue(listID);

            IMsgSetResponse responseMsgSet = qbSession.SendRequest(requestMsgSet);
            WalkListDelResponse(responseMsgSet, listID);
        }

        private void WalkListDelResponse(IMsgSetResponse responseMsgSet, string listID)
        {
            IResponseList responseList = responseMsgSet.ResponseList;
            if (responseList == null || responseList.Count == 0)
                return;

            IResponse response = responseList.GetAt(0);
            if (response.StatusCode == 0 && response.Detail != null)
            {
                Debug.WriteLine($"Successfully deleted Customer (ListID: {listID}).");
            }
            else
            {
                throw new Exception($"Error Deleting Customer (ListID: {listID}): {response.StatusMessage}. Status code: {response.StatusCode}");
            }
        }
    }
}
