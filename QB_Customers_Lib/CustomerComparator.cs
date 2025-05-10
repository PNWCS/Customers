


using System.Diagnostics;
using Serilog;
using System.Collections.Generic;
using System.Linq;

namespace QB_Customers_Lib
{
    public class CustomersComparator
    {
        // Static dictionary to store the last set of customers that were processed
        // This simulates what would normally be retrieved from QuickBooks
        private static Dictionary<string, Customer> _lastProcessedCustomers = new();

        public static List<Customer> CompareCustomers(List<Customer> companyCustomers)
        {
            Log.Information("CustomersComparator Initialized");

            // Create result list to store all customers with their determined status
            List<Customer> resultList = new List<Customer>();

            // Create dictionaries for faster lookups
            var companyDict = companyCustomers.ToDictionary(c => c.Company_ID);

            // First, find customers that exist in _lastProcessedCustomers but not in companyCustomers (Missing)
            foreach (var lastCustomer in _lastProcessedCustomers.Values)
            {
                if (!companyDict.ContainsKey(lastCustomer.Company_ID))
                {
                    // This customer was previously processed but is now removed from the company list
                    var missingCustomer = new Customer(
                        lastCustomer.Name,
                        lastCustomer.Fax,
                        lastCustomer.Company_ID)
                    {
                        QB_ID = lastCustomer.QB_ID,
                        Status = CustomerStatus.Missing
                    };
                    resultList.Add(missingCustomer);
                    Log.Information("Customer {Name} is Missing.", missingCustomer.Name);
                }
            }

            // Next, process all company customers
            foreach (var companyCustomer in companyCustomers)
            {
                Customer resultCustomer;

                if (_lastProcessedCustomers.TryGetValue(companyCustomer.Company_ID, out var lastCustomer))
                {
                    // Customer exists in both sets, check for differences
                    resultCustomer = new Customer(
                        companyCustomer.Name,
                        companyCustomer.Fax,
                        companyCustomer.Company_ID)
                    {
                        QB_ID = lastCustomer.QB_ID
                    };

                    if (lastCustomer.Name == companyCustomer.Name)
                    {
                        resultCustomer.Status = CustomerStatus.Unchanged;
                        Log.Information("Customer {Name} is Unchanged.", resultCustomer.Name);
                    }
                    else
                    {
                        resultCustomer.Status = CustomerStatus.Different;
                        Log.Information("Customer {Name} is Different.", resultCustomer.Name);
                    }
                }
                else
                {
                    // Customer is new (not in _lastProcessedCustomers)
                    resultCustomer = new Customer(
                        companyCustomer.Name,
                        companyCustomer.Fax,
                        companyCustomer.Company_ID)
                    {
                        // Simulate adding to QB and getting a QB_ID
                        QB_ID = "QB_" + companyCustomer.Company_ID,
                        Status = CustomerStatus.Added
                    };
                    Log.Information("Customer {Name} is Added.", resultCustomer.Name);
                }

                resultList.Add(resultCustomer);
            }

            // Update our cache for next comparison
            _lastProcessedCustomers = resultList
                .Where(c => c.Status != CustomerStatus.Missing)
                .ToDictionary(c => c.Company_ID);

            Log.Information("CustomersComparator Completed");
            return resultList;
        }
    }
}
