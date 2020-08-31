using iischef.core;
using System;
using System.Configuration.Install;
using System.Reflection;
using System.ServiceProcess;
using Console = System.Console;

namespace iischef.service
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        private static void Main(string[] args)
        {
            try
            {
                _Main(args);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error:" + e.Message);
                Console.WriteLine(e.StackTrace);
            }
        }

        private static void _Main(string[] args)
        {
            Console.WriteLine("Done");

            if (Environment.UserInteractive)
            {
                string parameter = string.Concat(args);
                Console.WriteLine("arguments: " + parameter);

                switch (parameter)
                {
                    case "--install":
                        ManagedInstallerClass.InstallHelper(new[] { Assembly.GetExecutingAssembly().Location });
                        Console.WriteLine("Install successful");
                        break;
                    case "--uninstall":
                        ManagedInstallerClass.InstallHelper(new[] { "/u", Assembly.GetExecutingAssembly().Location });
                        Console.WriteLine("Uninstall successful");
                        break;
                    case "--run":
                        var app = new ApplicationService(null, true);
                        app.Start();
                        break;
                    case "--test":
                        ChefService service1 = new ChefService();
                        service1.TestStartupAndStop(args);
                        break;
                }
            }
            else
            {
                ServiceBase[] servicesToRun;
                servicesToRun = new ServiceBase[]
                {
                   new ChefService()
                };
                ServiceBase.Run(servicesToRun);
            }
        }
    }
}
