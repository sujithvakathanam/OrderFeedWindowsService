using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using NUnit.Framework;
using TechTalk.SpecFlow;
using TechTalk.SpecFlow.Assist;
using WindowsService;
using System.Configuration;
using WindowsService.Helpers;

namespace IntegrationTests.StepDefinition
{
    [Binding]
    public class OrdersFeedStepDefinition
    {
        private readonly DataHelper _dataHelper;
        private string _machineNameLocal = string.Empty;

        public OrdersFeedStepDefinition()
        {
          _dataHelper = new DataHelper();
          _machineNameLocal = Environment.MachineName;
        }

        [Given(@"the (.*) directory contains no files")]
        public void GivenTheDirectoryContainsNoFiles(string exportFolder)
        {
            var folderPath = ConfigurationManager.AppSettings[exportFolder];
            ScenarioContext.Current["DropLocation"] = folderPath;

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }
            else
            {
                var files = Directory.GetFiles(folderPath);
                foreach (var file in files)
                {
                    File.Delete(file);
                }
            }
        }

        [Given(@"there are no messages in (.*)")]
        public void GivenThereAreNoMessagesInOrderfeedqueue(string qname)
        {
            _dataHelper.PurgeQueue(qname);
        }

        [Given(@"the following order has been shipped")]
        public void GivenTheFollowingOrderHasBeenShipped(Table table)
        {
            foreach (var row in table.Rows)
            {
                int orderId = Convert.ToInt32(row["OrderId"]);
                string customerId = Convert.ToString(row["CustomerId"]);
                int employeeId = Convert.ToInt32(row["EmployeeId"]);
                int OrderFeed = Convert.ToInt32(row["OrderFeed"]);
                _dataHelper.InsertIntoOrders(orderId,customerId,employeeId,OrderFeed);
            }

            //Below code is to clean up the data for 'AfterScenarioStep'
            string[] orders = table.Rows.Select(tableRow => tableRow["OrderId"]).ToArray();
            ScenarioContext.Current["OrdersList"] = orders;

        }
        
        [When(@"the OrderFeed Service is run")]
        public void WhenTheOrderFeedServiceIsRun(Table table)
        {
            foreach (var row in table.Rows)
            {
                string serviceName = Convert.ToString(row["ServiceName"]);
                RunOutBoundService(serviceName);
            }
        }

        [Then(@"the OrdersTable should be Updated with below OrderFeedStatus information")]
        public void ThenTheOrdersTableShouldBeUpdatedWithBelowOrderFeedStatusInformation(Table table)
        {
            string[] orders = table.Rows.Select(tableRow => tableRow["OrderId"]).ToArray();
            var actualData = _dataHelper.ResultsFromOrders(orders);
            table.CompareToSet(actualData.Select((x) => new {x.OrderID, x.OrderFeed}));
        }


        [Then(@"(.*) file should be generated with name (.*)")]
        public void ThenFileShouldBeGeneratedWithName(string fileCount, string expectedFileName)
        {
            
            Thread.Sleep(3000);
            var exportFileName = expectedFileName.Replace("<datetimestamp>", "");
            var exportFolder= ScenarioContext.Current["DropLocation"].ToString();
            var expectedFilePath = Path.Combine(exportFolder, exportFileName);

            var fileList = new List<string>();
            var files = Directory.GetFiles(exportFolder);
            Assert.AreEqual(fileCount,files.Count().ToString(CultureInfo.InvariantCulture));

            foreach (var fi in files.Select(file=> new FileInfo(file)))
            {
                fileList.Add(fi.FullName);
                Assert.IsTrue(fi.FullName.Contains(expectedFilePath));
            }
            ScenarioContext.Current["ExpectedFilePath"] = fileList;
        }

        [Then(@"the file should contain the following details")]
        public void ThenTheFileShouldContainTheFollowingDetails(string expectedContent)
        {
            expectedContent = Regex.Replace(expectedContent, @"\t", "    ");
            var fileList = (List<string>) ScenarioContext.Current["ExpectedFilePath"];
            if (fileList.Count > 0)
            {

                foreach (var file in fileList)
                {
                    var actualContent = File.ReadAllText(file);
                    Assert.AreEqual(actualContent.Trim(), expectedContent.Trim());
                }
            }
            else
            {
                Assert.Fail("No file is found");
            }
        }
        

        private void RunOutBoundService(string serviceName )
        {
           HelperClass.StopService(serviceName,_machineNameLocal, 1000);
           HelperClass.StartService(serviceName,_machineNameLocal,1000);
        }

        [Then(@"the below message should be raised in MSM queue")]
        public void ThenTheBelowMessageShouldBeRaisedInMSMQueue(Table table)
        {
            foreach (var row in table.Rows)
            {
                string qname = Convert.ToString(row["QueueName"]);
                string expectedMessage = Convert.ToString(row["MessageBody"]);
                string actualMessage = _dataHelper.RetrieveFromQueue(qname);
                Assert.AreEqual(expectedMessage, actualMessage);
                
            }

            //Below code is used for Purging up the Queues
            string[] qnamelist = table.Rows.Select(tableRow => tableRow["QueueName"]).ToArray();
            ScenarioContext.Current["QueueNameList"] = qnamelist;
        }

        [Then(@"no message should be raised in (.*) queue")]
        public void ThenNoMessageShouldBeRaisedInMSMQueue(string qname)
        {
            string expectedMessage = string.Empty;
            string actualMessage = _dataHelper.RetrieveFromQueue(qname);
            Assert.AreEqual(expectedMessage,actualMessage);
        }

    }
}
