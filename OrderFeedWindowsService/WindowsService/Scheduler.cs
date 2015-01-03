using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using WindowsService.Helpers;

namespace WindowsService
{
    public class Scheduler
    {
        System.Timers.Timer oTimer = null;
        private double interval = 20000;

        private readonly DataHelper _dataHelper;

        public Scheduler()
        {
           _dataHelper = new DataHelper();
        }
        public void Start()
        {
            oTimer = new Timer(interval);
            oTimer.AutoReset = true;
            oTimer.Enabled = true;
            oTimer.Start();
            oTimer.Elapsed += new System.Timers.ElapsedEventHandler(oTimer_Elapsed);
        }

        private void oTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            HelperClass.CreateXml(_dataHelper);
        }
    }
}
