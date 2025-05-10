using System;
using QB_Customers_Lib;
using QBFC16Lib;

namespace QB_Customers_CLI
{
    class DeleteAllCustomers
    {
        static void Main()
        {
            try
            {
                using var qbSession = new QuickBooksSession("QB Customer Cleanup Tool");

                // Step 1: Query all customers
                var allCustomers = CustomerReader.QueryAllCustomers();

                Console.WriteLine($"Found {allCustomers.Count} customers.");

                foreach (var customer in allCustomers)
                {
                    DeleteCustomer(qbSession, customer.QB_ID);
                }

                Console.WriteLine("All customers deleted.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error occurred: " + ex.Message);
            }
        }

        private static void DeleteCustomer(QuickBooksSession qbSession, string listID)
        {
            IMsgSetRequest req = qbSession.CreateRequestSet();
            IListDel del = req.AppendListDelRq();
            del.ListDelType.SetValue(ENListDelType.ldtCustomer);
            del.ListID.SetValue(listID);

            IMsgSetResponse resp = qbSession.SendRequest(req);

            IResponseList responses = resp.ResponseList;
            if (responses is null || responses.Count == 0) return;

            IResponse respItem = responses.GetAt(0);

            if (respItem.StatusCode == 0)
                Console.WriteLine($"Deleted Customer (ListID: {listID})");
            else
                Console.WriteLine($"Failed to delete (ListID: {listID}): {respItem.StatusMessage}");
        }
    }
}
