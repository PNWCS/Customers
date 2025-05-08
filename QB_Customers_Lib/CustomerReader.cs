//using Microsoft.VisualBasic;
//using QBFC16Lib;
//using Serilog;

//namespace QB_Customers_Lib
//{
//    public class Customer_Reader
//    {
//        public static List<Customer> QueryAllCustomers()
//        {
//            bool sessionBegun = false;
//            bool connectionOpen = false;
//            QBSessionManager sessionManager = null;

//            try
//            {
//                //Create the session Manager object
//                sessionManager = new QBSessionManager();

//                //Create the message set request object to hold our request
//                IMsgSetRequest requestMsgSet = sessionManager.CreateMsgSetRequest("US", 16, 0);
//                requestMsgSet.Attributes.OnError = ENRqOnError.roeContinue;

//                BuildCustomerQueryRq(requestMsgSet);

//                //Connect to QuickBooks and begin a session
//                sessionManager.OpenConnection("", "Sample Code from OSR");
//                connectionOpen = true;
//                sessionManager.BeginSession("", ENOpenMode.omDontCare);
//                sessionBegun = true;

//                //Send the request and get the response from QuickBooks
//                IMsgSetResponse responseMsgSet = sessionManager.DoRequests(requestMsgSet);

//                //End the session and close the connection to QuickBooks
//                sessionManager.EndSession();
//                sessionBegun = false;
//                sessionManager.CloseConnection();
//                connectionOpen = false;

//                WalkCustomerQueryRs(responseMsgSet);
//            }
//            catch (Exception e)
//            {
//                if (sessionBegun)
//                {
//                    sessionManager.EndSession();
//                }
//                if (connectionOpen)
//                {
//                    sessionManager.CloseConnection();
//                }

//                Console.WriteLine("Error: " + e.Message);
//            }
//            void BuildCustomerQueryRq(IMsgSetRequest requestMsgSet)
//            {
//                ICustomerQuery CustomerQueryRq = requestMsgSet.AppendCustomerQueryRq();
//            }

//            void WalkCustomerQueryRs(IMsgSetResponse responseMsgSet)
//            {
//                if (responseMsgSet == null) return;
//                IResponseList responseList = responseMsgSet.ResponseList;
//                if (responseList == null) return;
//                //if we sent only one request, there is only one response, we'll walk the list for this sample
//                for (int i = 0; i < responseList.Count; i++)
//                {
//                    IResponse response = responseList.GetAt(i);
//                    //check the status code of the response, 0=ok, >0 is warning
//                    if (response.StatusCode >= 0)
//                    {
//                        //the request-specific response is in the details, make sure we have some
//                        if (response.Detail != null)
//                        {
//                            //make sure the response is the type we're expecting
//                            ENResponseType responseType = (ENResponseType)response.Type.GetValue();
//                            if (responseType == ENResponseType.rtCustomerQueryRs)
//                            {
//                                //upcast to more specific type here, this is safe because we checked with response.Type check above
//                                ICustomerRetList CustomerRetList = (ICustomerRetList)response.Detail;
//                                for (int j = 0; j < CustomerRetList.Count; j++)
//                                {
//                                    ICustomerRet CustomerRet = CustomerRetList.GetAt(j);
//                                    (CustomerRet);
//                                }
//                            }
//                        }
//                    }
//                }
//            }
//        }



//    }
//    }

using System;
using System.Collections.Generic;
using Serilog;
using QBFC16Lib;
using Serilog;


namespace QB_Customers_Lib
{
    public class CustomerReader
    {
        static CustomerReader()
        {
            LoggerConfig.ConfigureLogging(); // Safe to call (only initializes once)
            Log.Information("CustomerReader Initialized.");
        }
        private static readonly ILogger Logger = Log.Logger;
        public static List<Customer> QueryAllCustomers()
        {
            bool sessionBegun = false;
            bool connectionOpen = false;
            QBSessionManager sessionManager = null;
            List<Customer> customers = new List<Customer>();

            try
            {
                //Create the session Manager object
                sessionManager = new QBSessionManager();

                //Create the message set request object to hold our request
                IMsgSetRequest requestMsgSet = sessionManager.CreateMsgSetRequest("US", 16, 0);
                requestMsgSet.Attributes.OnError = ENRqOnError.roeContinue;

                BuildCustomerQueryRq(requestMsgSet);

                //Connect to QuickBooks and begin a session
                sessionManager.OpenConnection("", AppConfig.QB_APP_NAME);
                connectionOpen = true;
                sessionManager.BeginSession("", ENOpenMode.omDontCare);
                sessionBegun = true;

                //Send the request and get the response from QuickBooks
                IMsgSetResponse responseMsgSet = sessionManager.DoRequests(requestMsgSet);

                //End the session and close the connection to QuickBooks
                sessionManager.EndSession();
                sessionBegun = false;
                sessionManager.CloseConnection();
                connectionOpen = false;

                customers = WalkCustomerQueryRs(responseMsgSet);
            }
            catch (Exception e)
            {
                if (sessionBegun)
                {
                    sessionManager.EndSession();
                }
                if (connectionOpen)
                {
                    sessionManager.CloseConnection();
                }

                Console.WriteLine("Error: " + e.Message);
            }

            return customers;

            void BuildCustomerQueryRq(IMsgSetRequest requestMsgSet)
            {
                ICustomerQuery CustomerQueryRq = requestMsgSet.AppendCustomerQueryRq();
            }

            List<Customer> WalkCustomerQueryRs(IMsgSetResponse responseMsgSet)
            {
                var customers = new List<Customer>();
                if (responseMsgSet == null) return customers;
                IResponseList responseList = responseMsgSet.ResponseList;
                if (responseList == null) return customers;
                //if we sent only one request, there is only one response, we'll walk the list for this sample
                for (int i = 0; i < responseList.Count; i++)
                {
                    IResponse response = responseList.GetAt(i);
                    //check the status code of the response, 0=ok, >0 is warning
                    if (response.StatusCode >= 0)
                    {
                        //the request-specific response is in the details, make sure we have some
                        if (response.Detail != null)
                        {
                            //make sure the response is the type we're expecting
                            ENResponseType responseType = (ENResponseType)response.Type.GetValue();
                            if (responseType == ENResponseType.rtCustomerQueryRs)
                            {
                                //upcast to more specific type here, this is safe because we checked with response.Type check above
                                ICustomerRetList CustomerRetList = (ICustomerRetList)response.Detail;
                                for (int j = 0; j < CustomerRetList.Count; j++)
                                {
                                    ICustomerRet CustomerRet = CustomerRetList.GetAt(j);
                                    if (CustomerRet != null)
                                    {
                                        var name = CustomerRet.Name != null ? CustomerRet.Name.GetValue() : string.Empty;
                                        var companyName = CustomerRet.CompanyName != null ? CustomerRet.CompanyName.GetValue() : string.Empty;
                                        var id = CustomerRet.ListID != null ? CustomerRet.ListID.GetValue() : string.Empty;
                                        var customer = new Customer(name, companyName);
                                        customer.QB_ID = id;
                                        customers.Add(customer);

                                        //Console.WriteLine($"Customer Name: {customer.Name}, Fax: {customer.Fax}");
                                        Log.Information("Successfully retrieved {Name} from QB", customer.Name, customer.CompanyName);
                                        Log.Information("Customer Name: {Name}, CompanyName: {companyName}", customer.Name, customer.CompanyName);


                                    }
                                }
                            }
                        }
                    }
                }
                Log.Information("CustomerReader Completed.");
                return customers;
            }
        }
    }
}
