using System.Diagnostics;
using Serilog;
using QB_Customers_Lib;
using QBFC16Lib;
using static QB_Customers_Test.CommonMethods;  // for EnsureLogFileClosed, DeleteOldLogFiles, etc.
using Xunit;

namespace QB_Customers_Test
{
    [Collection("Sequential Tests")]
    public class CustomerAdderTests
    {
        [Fact]
        public void AddMultipleCustomers_UsingCustomerAdder_AndVerifyInQB_And_ValidateLogs()
        {
            // 1) Prep: ensure Serilog has closed the file, remove old logs, reset logger
            EnsureLogFileClosed();
            DeleteOldLogFiles();
            ResetLogger();

            // 2) Build a list of random Customer objects (Name = random, Fax = simulated "Company ID")
            const int CUSTOMER_COUNT = 5;
            const int STARTING_COMPANY_ID = 200;
            var customersToAdd = new List<Customer>();
            for (int i = 0; i < CUSTOMER_COUNT; i++)
            {
                string randomName = "AdderTestCustomer_" + Guid.NewGuid().ToString("N").Substring(0, 8);
                int companyID = STARTING_COMPANY_ID + i;
                string fax = companyID.ToString(); // reusing Fax field for our "Company ID"
                customersToAdd.Add(new Customer(randomName, fax));
            }

            // 3) Call the method under test: CustomerAdder.AddCustomers(...)
            //    (Assumes your Lib project has something like this. Also assumes it sets Customer.QB_ID or otherwise
            //     populates some property so the test can verify the result.)
            CustomerAdder.AddCustomers(customersToAdd);

            // 4) Verify each newly added customer actually exists in QuickBooks by direct QBFC calls (no Reader code).
            using (var qbSession = new QuickBooksSession(AppConfig.QB_APP_NAME))
            {
                foreach (var cust in customersToAdd)
                {
                    Assert.False(string.IsNullOrEmpty(cust.QB_ID),
                                 $"CustomerAdder did not set QB_ID for {cust.Name}.");

                    var qbCustomer = QueryCustomerByListID(qbSession, cust.QB_ID);
                    Assert.NotNull(qbCustomer);
                    Assert.Equal(cust.Name, qbCustomer?.Name);
                    Assert.Equal(cust.Fax, qbCustomer?.Fax);
                }
            }

            // 5) Cleanup: remove the test customers from QB.
            using (var qbSession = new QuickBooksSession(AppConfig.QB_APP_NAME))
            {
                foreach (var cust in customersToAdd.Where(c => !string.IsNullOrEmpty(c.QB_ID)))
                {
                    DeleteCustomer(qbSession, cust.QB_ID);
                }
            }

            // 6) Ensure logs have been written and closed.
            EnsureLogFileClosed();
            string logFile = GetLatestLogFile();
            EnsureLogFileExists(logFile);
            string logContents = File.ReadAllText(logFile);

            // 7) Verify that the Adder wrote expected log messages.
            //    (Adjust these strings to match what your CustomerAdder class actually logs.)
            Assert.Contains("CustomerAdder Initialized", logContents);
            Assert.Contains("CustomerAdder Completed", logContents);
            foreach (var cust in customersToAdd)
            {
                // Example: If your Adder logs something like “Successfully added [Name] with ListID [XYZ]”
                string expectedAddMsg = $"Successfully added {cust.Name} to QuickBooks";
                Assert.Contains(expectedAddMsg, logContents);
            }
        }

        /// <summary>
        /// Queries QuickBooks directly (without using the Reader code) for a single customer
        /// by ListID and returns a Customer object if found, or null if not found.
        /// </summary>
        private Customer? QueryCustomerByListID(QuickBooksSession qbSession, string listID)
        {
            IMsgSetRequest requestMsgSet = qbSession.CreateRequestSet();
            ICustomerQuery customerQueryRq = requestMsgSet.AppendCustomerQueryRq();
            customerQueryRq.ORCustomerListQuery.ListIDList.Add(listID);

            IMsgSetResponse responseMsgSet = qbSession.SendRequest(requestMsgSet);
            IResponseList responseList = responseMsgSet.ResponseList;
            if (responseList == null || responseList.Count == 0) return null;

            IResponse response = responseList.GetAt(0);
            if (response.StatusCode != 0) return null; // something went wrong

            // CustomerRet list can contain multiple customers, but we only asked for one ListID.
            ICustomerRetList? custRetList = response.Detail as ICustomerRetList;
            if (custRetList == null || custRetList.Count == 0) return null;

            ICustomerRet custRet = custRetList.GetAt(0);
            // Convert ICustomerRet fields into your Customer model
            var found = new Customer(
                name: custRet.Name?.GetValue() ?? "",
                fax: custRet.Fax?.GetValue() ?? ""
            );
            found.QB_ID = custRet.ListID?.GetValue() ?? "";
            return found;
        }

        /// <summary>
        /// Directly deletes a customer from QuickBooks by its ListID (no Reader code).
        /// </summary>
        private void DeleteCustomer(QuickBooksSession qbSession, string listID)
        {
            IMsgSetRequest requestMsgSet = qbSession.CreateRequestSet();
            IListDel listDelRq = requestMsgSet.AppendListDelRq();
            listDelRq.ListDelType.SetValue(ENListDelType.ldtCustomer);
            listDelRq.ListID.SetValue(listID);

            IMsgSetResponse responseMsgSet = qbSession.SendRequest(requestMsgSet);
            IResponseList responseList = responseMsgSet.ResponseList;
            if (responseList == null || responseList.Count == 0) return;

            IResponse response = responseList.GetAt(0);
            if (response.StatusCode == 0)
            {
                Debug.WriteLine($"Successfully deleted Customer (ListID: {listID}).");
            }
            else
            {
                throw new Exception($"Error Deleting Customer (ListID: {listID}): {response.StatusMessage} " +
                                    $"(Status code: {response.StatusCode}).");
            }
        }
    }
}
