using QBFC16Lib;
using Serilog;

namespace QB_Customers_Lib
{
    public class CustomerAdder

    {

        static CustomerAdder()
        {
            LoggerConfig.ConfigureLogging(); // Safe to call (only initializes once)
            Log.Information("CustomerAdder Initialized.");
            // Initialize the QuickBooks session manager here if needed.
        }
        public static void AddCustomers(List<Customer> custinfo)
        {

            using (var qbSession = new QuickBooksSession(AppConfig.QB_APP_NAME))
            {
                foreach (var customer in custinfo)
                {
                    string qbID = AddCustomer(qbSession, customer.Name, customer.CompanyName);
                    customer.QB_ID = qbID; // Store the returned QB ListID.
                }
            }
            Log.Information("CustomerAdder Completed");
        }

        private static string AddCustomer(QuickBooksSession qbSession, string name, string companyName)
        {
            IMsgSetRequest requestMsgSet = qbSession.CreateRequestSet();
            ICustomerAdd customerAddRq = requestMsgSet.AppendCustomerAddRq();
            customerAddRq.Name.SetValue(name);
            customerAddRq.CompanyName.SetValue(companyName);
            // Additional customer fields can be set here as needed.

            IMsgSetResponse responseMsgSet = qbSession.SendRequest(requestMsgSet);
            return ExtractListIDFromResponse(responseMsgSet);
        }

        private static string ExtractListIDFromResponse(IMsgSetResponse responseMsgSet)
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
    }
}