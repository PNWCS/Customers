// QB_Customers_Test/CustomersComparatorTests.cs
using System.Diagnostics;
using Serilog;
using QB_Customers_Lib;
using QB_Customers_Lib;     // ← where your CustomersComparator lives
using QBFC16Lib;
using static QB_Customers_Test.CommonMethods;
using System.Xml.Linq;

namespace QB_Customers_Test
{
    [Collection("Sequential Tests")]
    public class CustomersComparatorTests
    {
        [Fact]
        public void CompareCustomers_InMemoryScenario_And_Verify_Logs()
        {
            // ── 0. housekeeping ───────────────────────────────────────────────
            EnsureLogFileClosed();
            DeleteOldLogFiles();
            ResetLogger();

            // ── 1. create five unique customers in memory (company file) ─────
            const int START_ID = 10_000;
            var initialCustomers = new List<Customer>();

            for (int i = 0; i < 5; i++)
            {
                string suffix = Guid.NewGuid().ToString("N")[..8];
                initialCustomers.Add(new Customer(
                    $"TestCust_{suffix}",
                    $"TestCo_{suffix}",
                    $"{START_ID + i}"));
                Debug.WriteLine($"Customer {i}: {initialCustomers[i].Name}");
            }

            List<Customer> firstCompareResult  = new();
            List<Customer> secondCompareResult = new();

            try
            {
                // ── 2. first compare – expect every customer to be Added ─────
                firstCompareResult = CustomersComparator.CompareCustomers(initialCustomers);
                Debug.WriteLine("First compare result");
                foreach (var customer in firstCompareResult)
                {
                    Debug.WriteLine(customer);
                }
                //Debug.WriteLine("Initial Customers ");
                //for (int i = 0;i<initialCustomers.Count; i++)
                //{
                //    Debug.WriteLine(initialCustomers[i]);
                //}

                foreach (var c in firstCompareResult
                                 .Where(c => initialCustomers.Any(x => x.Company_ID == c.Company_ID)))
                {
                    Assert.Equal(CustomerStatus.Added, c.Status);
                }

                // ── 3. mutate list: remove one   ➜ Missing
                //                  rename one     ➜ Different
                var updated = new List<Customer>(initialCustomers);
                var removed = updated[0];
                var renamed = updated[1];

                updated.Remove(removed);
                renamed.Name += "_Renamed";

                // ── 4. second compare – expect Missing, Different, Unchanged ─
                secondCompareResult = CustomersComparator.CompareCustomers(updated);

                Debug.WriteLine("Second compare result");
                foreach (var customer in secondCompareResult)
                {
                    Debug.WriteLine(customer);
                }

                var dict = secondCompareResult.ToDictionary(c => c.Company_ID);

                Assert.Equal(CustomerStatus.Missing, dict[removed.Company_ID].Status);
                Assert.Equal(CustomerStatus.Different, dict[renamed.Company_ID].Status);

                foreach (var id in updated
                                   .Select(c => c.Company_ID)
                                   .Except(new[] { renamed.Company_ID }))
                {
                    Assert.Equal(CustomerStatus.Unchanged, dict[id].Status);
                }
            }
            finally
            {
                // ── 5. clean up QB (remove Added customers) ───────────────────
                var added = firstCompareResult?
                            .Where(c => !string.IsNullOrEmpty(c.QB_ID))
                            .ToList();

                if (added is { Count: >0 })
                {
                    using var qb = new QuickBooksSession(AppConfig.QB_APP_NAME);
                    foreach (var c in added)
                        DeleteCustomer(qb, c.QB_ID);
                }
            }

            // ── 6. verify logs ────────────────────────────────────────────────
            EnsureLogFileClosed();
            string logFile = GetLatestLogFile();
            EnsureLogFileExists(logFile);
            string logs = File.ReadAllText(logFile);

            Assert.Contains("CustomersComparator Initialized", logs);
            Assert.Contains("CustomersComparator Completed",   logs);

            void AssertLogged(IEnumerable<Customer> customers)
            {
                foreach (var c in customers)
                    Assert.Contains($"Customer {c.Name} is {c.Status}.", logs);
            }

            AssertLogged(firstCompareResult);
            AssertLogged(secondCompareResult);
        }

        // ───────────────────── helper: delete customer from QB ──────────────
        private static void DeleteCustomer(QuickBooksSession qbSession, string listID)
        {
            IMsgSetRequest req = qbSession.CreateRequestSet();
            IListDel del = req.AppendListDelRq();
            del.ListDelType.SetValue(ENListDelType.ldtCustomer);
            del.ListID.SetValue(listID);

            IMsgSetResponse resp = qbSession.SendRequest(req);
            WalkListDelResponse(resp, listID);
        }

        private static void WalkListDelResponse(IMsgSetResponse respSet, string listID)
        {
            IResponseList responses = respSet.ResponseList;
            if (responses is null || responses.Count == 0) return;

            IResponse resp = responses.GetAt(0);
            Debug.WriteLine(resp.StatusCode == 0
                ? $"Successfully deleted Customer (ListID: {listID})."
                : $"Error deleting Customer: {resp.StatusMessage}");
        }
    }
}
