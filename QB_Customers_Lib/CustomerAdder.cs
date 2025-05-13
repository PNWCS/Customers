using System.Diagnostics;
using QBFC16Lib;
using Serilog;

namespace QB_Customers_Lib
{
    public class CustomerAdder
    {
        // QuickBooks field length limits
        private const int QB_NAME_MAX_LENGTH = 20;
        private const int QB_FAX_MAX_LENGTH = 20;

        static CustomerAdder()
        {
            LoggerConfig.ConfigureLogging(); // Safe to call (only initializes once)
            Log.Information("CustomerAdder Initialized.");
        }

        public static void AddCustomers(List<Customer> custinfo)
        {
            using (var qbSession = new QuickBooksSession(AppConfig.QB_APP_NAME))
            {
                foreach (var customer in custinfo)
                {
                    string qbID = AddCustomer(qbSession, customer.Name, customer.Fax, customer.Company_ID);
                    customer.QB_ID = qbID; // Store the returned QB ListID.
                }
            }
            Log.Information("CustomerAdder Completed");
        }

        private static string AddCustomer(QuickBooksSession qbSession, string name, string fax, string companyId)
        {
            // Truncate values to field limits
            name = name?.Length > QB_NAME_MAX_LENGTH ? name[..QB_NAME_MAX_LENGTH] : name;
            fax = fax?.Length > QB_FAX_MAX_LENGTH ? fax[..QB_FAX_MAX_LENGTH] : fax;

            IMsgSetRequest requestMsgSet = qbSession.CreateRequestSet();
            ICustomerAdd customerAddRq = requestMsgSet.AppendCustomerAddRq();
            Debug.WriteLine($"Adding Customer: {name}, Fax: {fax}");
            customerAddRq.Name.SetValue(name);
            customerAddRq.Fax.SetValue(fax);
            customerAddRq.AccountNumber.SetValue(companyId.ToString());
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
