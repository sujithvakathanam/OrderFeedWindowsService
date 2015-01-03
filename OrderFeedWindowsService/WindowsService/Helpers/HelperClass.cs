using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using WindowsService.Helpers;

namespace WindowsService.Helpers
{
  public static class HelperClass
    {
       public static void CreateXml(DataHelper _datahelper)
        {
           string fileName = "OrderFeed.xml".AppendTimeStamp();
            var folderPath = ConfigurationManager.AppSettings["ExportFolder_OrdersFeed"];
           var actualfolder = folderPath.ToString();
           var actualFilePath = Path.Combine(actualfolder, fileName);
           
           XDocument tdoc = new XDocument(
                new XDeclaration("1.0", "utf-8", "yes"),
                new XComment("Order Feed information"));
           
            XElement orders = new XElement(new XElement("Orders"));

           if (_datahelper.GetShippedOrders().Any())
           {
               foreach (var order in _datahelper.GetShippedOrders())
               {
                   string customerId = Convert.ToString(order.CustomerID);
                   string shipName = Convert.ToString(order.ShipName);
                   string shipCountry = Convert.ToString(order.ShipCountry);
                   string orderId = Convert.ToString(order.OrderID);
                   orders.Add(new XElement("Order",
                                           new XElement("OrderId",
                                                        orderId),
                                           new XElement("CustomerId",
                                                        customerId),
                                           new XElement("ShipName",
                                                        shipName),
                                           new XElement("ShipCountry",
                                                        shipCountry)));
                   orders.TrimWhiteSpaceFromValues();
                   _datahelper.UpdateOrderFeed(order.OrderID);
                   _datahelper.RaiseEvent(order.OrderID,order.CustomerID);
               }
               tdoc.Add(orders);
               tdoc.Save(actualFilePath, SaveOptions.None);
           }
        }


      public static void StartService(string serviceName,string machineName, int maxWaitTime)
      {
          var serviceController = new ServiceController(serviceName, machineName);
          if (serviceController.Status != ServiceControllerStatus.Running)
          {
              serviceController.Start();
              WaitForServiceToStart(serviceName, machineName, maxWaitTime);
          }

      }

      private static void WaitForServiceToStart(string serviceName, string machineName, int maxWaitTime)
      {
          Stopwatch stopwatch = new Stopwatch();
          stopwatch.Start();
          var serviceController = new ServiceController(serviceName, machineName);
          var serviceStatus = serviceController.Status;
          while (serviceStatus != ServiceControllerStatus.Running && stopwatch.ElapsedMilliseconds < maxWaitTime)
          {
              Thread.Sleep(1000);
              serviceStatus = (new ServiceController(serviceName, machineName)).Status;
          }
      }

      public static void StopService(string serviceName, string machineName, int maxWaitTime)
      {
          var serviceController = new ServiceController(serviceName, machineName);

          if (serviceController.Status != ServiceControllerStatus.Stopped)
          {
              try
              {
                  serviceController.Stop();

              }
              catch (Win32Exception ex)
              {
                    
                  KillService(serviceName);
              }
              
          }
          WaitForSeriveToStop(serviceName, machineName, maxWaitTime);

      }

      private static void WaitForSeriveToStop(string serviceName, string machineName, int maxWaitTime)
      {
          Stopwatch stopwatch = new Stopwatch();
          stopwatch.Start();
          var serviceController = new ServiceController(serviceName, machineName);
          var serviceStatus = serviceController.Status;
          while (serviceStatus != ServiceControllerStatus.Stopped && stopwatch.ElapsedMilliseconds < maxWaitTime)
          {
            Thread.Sleep(1000);
             serviceStatus = (new ServiceController(serviceName, machineName)).Status;
          }
          serviceStatus = (new ServiceController(serviceName, machineName)).Status;
          if (serviceStatus != ServiceControllerStatus.Stopped)
          {
              KillService(serviceName);
          }
      }

      private static void KillService(string serviceName)
      {
          Process[] process = Process.GetProcessesByName(serviceName);
          for(int i = 0; i < process.Length; i++)
          {
              process[i].Kill();
          }
      }

      //protected string DateValidation(string date, int noOfDaysToAdd)
      //{
      //    DateTimeFormatInfo dtfi = new DateTimeFormatInfo();
      //    dtfi.ShortDatePattern = "dd-MM-yyyy";
      //    dtfi.DateSeparator = "-";
      //    DateTime mydt = Convert.ToDateTime(date, dtfi);
      //    DateTime todaysdt = DateTime.Today;
      //    if (mydt < todaysdt)
      //    {
      //        mydt = todaysdt.AddDays(noOfDaysToAdd);
      //    }
      //    string myDate = mydt.ToString("d/MM/yyyy");
      //    return myDate;
      //}




    }

  public static class MyExtensions
    {
        public static string AppendTimeStamp(this string fileName)
        {

            return string.Concat(Path.GetFileNameWithoutExtension(fileName),
                DateTime.Now.ToString("yyyyMMddHHmmssfff"),
                Path.GetExtension(fileName));
        }
    }


  public static class XElementExtensions
  {
      public static void TrimWhiteSpaceFromValues(this XElement element)
      {
          foreach (var descendent in element.Descendants())
          {
              if (!descendent.HasElements)
              {
                  descendent.SetValue(descendent.Value.Trim());
              }
              else
              {
                  descendent.TrimWhiteSpaceFromValues();
              }
          }
      }
  }

}

