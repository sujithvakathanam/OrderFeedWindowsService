using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TechTalk.SpecFlow;
using WindowsService.Helpers;

namespace IntegrationTests.StepDefinition
{
    [Binding]
    public class BaseStepDefinitions
    {

        [BeforeScenario()]
        public void BeforeScenario()
        {
            //TODO: implement logic that has to run before executing each scenario
        }


        [AfterScenario("PurgeMessage")]
        public void PurgeMessageAfterScenario()
        {
            var dataHelper = new DataHelper();
            IEnumerable<dynamic> qnames = (IEnumerable<dynamic>)ScenarioContext.Current["QueueNameList"];
            foreach (var item in qnames)
            {
                dataHelper.PurgeQueue(item);
            }
        
        }

        [AfterScenario("OrderFeed")]
        public void DeleteOrderAfterScenario()
        {
            var datahelper = new DataHelper();
            IEnumerable<dynamic> orderlist = (IEnumerable<dynamic>)ScenarioContext.Current["OrdersList"];
            foreach (var item in orderlist)
            {
                datahelper.DeleteOrders(item);
            }
        }
    }
}
