using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.Xml.Linq;
using Dapper;
using WindowsService.EventHelpers;
using System.Messaging;

namespace WindowsService.Helpers
{
    public class DataHelper
    {
        private readonly string _connectionStringNorthWind;
   
        
        public DataHelper()
        {
            _connectionStringNorthWind = ConfigurationManager.ConnectionStrings["northwind"].ConnectionString;    
        }

        public void InsertIntoOrders(int orderId,string customerId, int employeeId,int orderFeed)
        {
            var insertIntoOrders =
                "delete from orders where OrderId = @OrderId; " +
                "Set Identity_Insert Orders ON; " +
                "Insert into Orders(OrderID,CustomerID,EmployeeID,OrderDate,RequiredDate,ShippedDate,ShipVia,Freight, " +
                "ShipName,ShipAddress,ShipCity,ShipRegion,ShipPostalCode,ShipCountry,OrderFeed) " +
                "values(@OrderID,@CustomerID,@EmployeeID,@OrderDate,@RequiredDate,@ShippedDate,@ShipVia,@Freight, " +
                "@ShipName,@ShipAddress,@ShipCity,@ShipRegion,@ShipPostalCode,@ShipCountry,@OrderFeed)" +
                "Set Identity_Insert Orders OFF;";

            using (var connection = new SqlConnection(_connectionStringNorthWind))
            {
                connection.Execute(insertIntoOrders, new
                    {
                        @OrderID = orderId,
                        @CustomerID = customerId,
                        @EmployeeID = employeeId,
                        @OrderDate = DateTime.Now,
                        @RequiredDate = DateTime.Now,
                        @ShippedDate = DateTime.Now,
                        @ShipVia = 2,
                        @Freight = 30.00,
                        @ShipName = "Queen Cozinhaa",
                        @ShipAddress = "Walserweg 21",
                        @ShipCity = "Seattle",
                        @ShipRegion = "Lara",
                        @ShipPostalCode = "98124",
                        @ShipCountry ="USA",
                        @OrderFeed = orderFeed
                    });
            }
        }

        public void UpdateOrderFeed(int orderId)
        {
            var updateOrders = "update Orders set OrderFeed = @OrderFeed where orderId = @OrderId;";
            using (var connection = new SqlConnection(_connectionStringNorthWind))
            {
                connection.Execute(updateOrders, new
                    {
                        @OrderId = orderId,
                        @OrderFeed = 1
                    });
            }
        }


        public void DeleteOrders(string orderId)
        {
            const string deleteOrders = "Delete Orders where orderId = @OrderId;";
            using (var connection = new SqlConnection(_connectionStringNorthWind))
            {
                connection.Execute(deleteOrders, new
                {
                    @OrderId = Convert.ToInt32(orderId),
                });
            }
        }

        public void RaiseEvent(int orderId, string customerId)
        {
            var Worker = new OrderWorkerClass();
            Worker.OrderWorkPerformed += Worker_OrderWorkPerformed;
            Worker.DoWork(orderId, customerId);

        }

        static void Worker_OrderWorkPerformed(object sender, OrdersEventArgsClass e)
        {
            /*An event had been raised for the Cusotmer {0} with Orderid {1}",e.OrderId,e.CustomerId)*/
            PushIntoQueue(e.OrderId, e.CustomerId);
        }


        static void PushIntoQueue(int orderId, string customerId)
        {
            MessageQueue objMessageQueue = null;
            Message objMqMessage = new Message();
            string order = orderId.ToString();
            string strMessage = ("Event raised for Customer " + customerId + " with OrderId as " + order);
            objMessageQueue = MessageQueue.Exists(@".\Private$\OrderFeedQueue") ? new System.Messaging.MessageQueue(@".\Private$\OrderFeedQueue") : MessageQueue.Create(@".\Private$\OrderFeedQueue");
            //((XmlMessageFormatter)objMessageQueue.Formatter).TargetTypeNames = new string[] { "System.String,mscorlib" };
            //System.Messaging.XmlMessageFormatter.Write(objMqMessage, strMessage);
            XmlMessageFormatter formatter = new XmlMessageFormatter();
            formatter.Write(objMqMessage, strMessage);
            //objMqMessage.Body = strMessage;
            objMqMessage.Label = "ShippedOrderFeedData";
            objMessageQueue.Send(objMqMessage);
        }

        public string RetrieveFromQueue(string qname)
        {
            MessageQueue oMessageQueue = null;
            Message oMessage = new Message();
            string retrivedmsg = string.Empty;

            string queue = @".\Private$\" + qname;

            if (MessageQueue.Exists(queue))
            {
                oMessageQueue = new MessageQueue(queue);
                oMessageQueue.SetPermissions("Everyone", MessageQueueAccessRights.FullControl);
                var messages = oMessageQueue.GetAllMessages();

                if (messages.Any())
                {
                    try
                    {
                        oMessage = messages[0];
                        if (oMessage.Label == "ShippedOrderFeedData")
                        {
                            oMessage.Formatter = new XmlMessageFormatter(new string[] {"System.String,mscorlib"});
                            retrivedmsg = oMessage.Body.ToString();
                        }
                    }
                    catch (NullReferenceException e)
                    {
                        Console.WriteLine(e.Message);
                    }
                }
            }
            return retrivedmsg;
        }

        public void PurgeQueue(string qname)
        {
            MessageQueue obMessageQueue = null;
            Message obMessage = new Message();
            string queuetobepurged = @".\Private$\" + qname;

            if (MessageQueue.Exists(queuetobepurged))
            {
                obMessageQueue = new MessageQueue(queuetobepurged);
                obMessageQueue.SetPermissions("Everyone", MessageQueueAccessRights.FullControl);
                obMessageQueue.Purge();
            }
        }
        

        public IEnumerable<dynamic> GetShippedOrders()
        {
            using (var connection = new SqlConnection(_connectionStringNorthWind))
            {
                return
                    connection.Query("select * from Orders where ShippedDate IS NOT Null and OrderFeed <> 1")
                              .ToList();
            }
        }

        public IEnumerable<dynamic> ResultsFromOrders(string[] orders)
        {
            var order = BuildValuesForSqlStatement(orders);
            var sql = string.Format(
                @"select OrderID,OrderFeed from dbo.Orders
                 where OrderID in ({0})",order);
            using (var connection = new SqlConnection(_connectionStringNorthWind))
            {
                return connection.Query(sql).ToList();
            }
        }

        private string BuildValuesForSqlStatement(string[] values)
        {
            var builder = new StringBuilder();
            foreach (var value in values)
            {
                if (values.Last() == value)
                {
                    builder.Append("'" + value + "'");
                    break;
                }

                builder.Append("'" + value + "',");
            }
            return builder.ToString();
        }

        
        
    }
}
