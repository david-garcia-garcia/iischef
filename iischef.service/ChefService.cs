using iischef.core;
using System.ServiceProcess;
using Console = System.Console;

namespace iischef.service
{
    /// <summary>
    /// Main chef service
    /// </summary>
    public partial class ChefService : ServiceBase
    {
        /// <summary>
        /// Monitor instance.
        /// </summary>
        protected ApplicationService App;

        /// <summary>
        /// Get an instance of ChefService
        /// </summary>
        public ChefService()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Test startup
        /// </summary>
        /// <param name="args"></param>
        public void TestStartupAndStop(string[] args)
        {
            this.OnStart(args);
            Console.ReadLine();
            this.OnStop();
        }

        protected override void OnStart(string[] args)
        {
            // El método OnStart debe volver al sistema operativo después 
            // de que haya comenzado el funcionamiento del servicio.No debe bloquearse ni ejecutar un bucle infinito.
            this.App = new ApplicationService();
            this.App.Start();
        }

        protected override void OnStop()
        {
            // Stop the service!
            this.App?.Stop();
        }
    }
}
