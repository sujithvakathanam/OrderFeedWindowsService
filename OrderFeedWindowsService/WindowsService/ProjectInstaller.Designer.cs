namespace WindowsService
{
    partial class ProjectInstaller
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.OrderFeedProcessInstaller = new System.ServiceProcess.ServiceProcessInstaller();
            this.OrderFeedserviceInstaller = new System.ServiceProcess.ServiceInstaller();
            // 
            // OrderFeedProcessInstaller
            // 
            this.OrderFeedProcessInstaller.Account = System.ServiceProcess.ServiceAccount.LocalSystem;
            this.OrderFeedProcessInstaller.Password = null;
            this.OrderFeedProcessInstaller.Username = null;
            // 
            // OrderFeedserviceInstaller
            // 
            this.OrderFeedserviceInstaller.ServiceName = "OrderFeedService";
            this.OrderFeedserviceInstaller.StartType = System.ServiceProcess.ServiceStartMode.Automatic;
            // 
            // ProjectInstaller
            // 
            this.Installers.AddRange(new System.Configuration.Install.Installer[] {
            this.OrderFeedProcessInstaller,
            this.OrderFeedserviceInstaller});

        }

        #endregion

        private System.ServiceProcess.ServiceInstaller OrderFeedserviceInstaller;
        public System.ServiceProcess.ServiceProcessInstaller OrderFeedProcessInstaller;
    }
}