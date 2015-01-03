using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Configuration;
using Dapper;
using CustomerOrders;


namespace Helpers
{
    /*Below Class represents DAL--Data Access Layer*/
    public class DAL
    {
        private readonly string _connectionStringNorthWind;
        public IEnumerable<dynamic> Orderslist;
        private ErrorHandler errorHandler;
        
        public DAL()
        {
            _connectionStringNorthWind = ConfigurationManager.ConnectionStrings["northwind"].ConnectionString;
            errorHandler = new ErrorHandler();
        }

        public IEnumerable<dynamic> GetShippedOrdersWithNoOrderFeed()
        {
            
            using (var connection = new SqlConnection(_connectionStringNorthWind))
            {
                return connection.Query("select * from Orders where ShippedDate IS NOT Null and OrderFeed <> 1").ToList();
                
            }
            
        }
        
        public Order GetOrder(int id)
        {
            try
            {
                if (Orderslist == null)
                {
                    Orderslist = GetOrdersList();
                }

                foreach (var order in Orderslist.Where(order => order.OrderID == id))
                {
                    return order;
                }

                return null;

                //Below foreach is converted into LINQ as above
                //foreach (var order in Orderslist)
                //{
                //    if (order.OrderID == id)
                //    {
                //        return order;
                //    }
                //}

            }
            catch (Exception ex)
            {
                errorHandler.ErrorMessage = ex.Message.ToString();
                throw;
            }
        
        }
        
        //Method to Get list of all Orders
        private IEnumerable<dynamic> GetOrdersList()
        {
            try
            {
                using (var connection = new SqlConnection(_connectionStringNorthWind))
                {
                  Orderslist= connection.Query("select * from Orders").ToList();
                  return Orderslist;
                }
            }
            catch (Exception ex)
            {
                errorHandler.ErrorMessage = ex.Message.ToString();
                throw;
            }
        }
        
        //Summary-Returns error message//
        public string GetException()
        {
            return errorHandler.ErrorMessage.ToString();
        }
    }
}
