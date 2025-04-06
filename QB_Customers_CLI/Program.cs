

using System.Net.NetworkInformation;
using QB_Customers_Lib;
//using Customer_LIB;

namespace Customers
{


    class Program
    {
        static void Main(string[] args)
        {
            CustomerReader.QueryAllCustomers();
        }
    }
}