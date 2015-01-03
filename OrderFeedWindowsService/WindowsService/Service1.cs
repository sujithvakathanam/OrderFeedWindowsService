using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using WindowsService.Helpers;

namespace WindowsService
{
    public partial class Service1 : ServiceBase
    {
        private readonly DataHelper _dataHelper;
        
        public Service1()
        {
            _dataHelper = new DataHelper();
            InitializeComponent();
            InitializeScheduler();
        }

        private void InitializeScheduler()
        {
            Scheduler scheduler = new Scheduler();
            scheduler.Start();
        }

        public void OnDebug()
        {
        OnStart(null);
        }

        protected override void OnStart(string[] args)
        {
            
            HelperClass.CreateXml(_dataHelper);
        }
        
        
        protected override void OnStop()
        {
            System.IO.File.Create(AppDomain.CurrentDomain.BaseDirectory + "OnStopfilecreation.txt");
        }
        
    }
}
