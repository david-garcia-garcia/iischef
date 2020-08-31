using iischef.logger;
using System;
using System.Threading;

namespace iischef.core
{
    /// <summary>
    /// Runs the Application in a timer-like
    /// loop.
    /// </summary>
    public class ApplicationService
    {
        /// <summary>
        /// Thread for the loop
        /// </summary>
        protected Thread LoopThread;

        /// <summary>
        /// Stop signaling.
        /// </summary>
        protected bool StopSignal = false;

        /// <summary>
        /// If the loop has stopped.
        /// </summary>
        protected bool Stopped = false;

        /// <summary>
        /// 30 seconds between loops.
        /// </summary>
        protected int Sleep = 30000;

        /// <summary>
        /// Settings file.
        /// </summary>
        protected string SettingsFile;

        /// <summary>
        /// Service logger.
        /// </summary>
        protected ILoggerInterface Logger;

        /// <summary>
        /// If we are running in the console
        /// </summary>
        protected bool Console;

        /// <summary>
        /// Create an Application service
        /// </summary>
        /// <param name="settingsFile">The settings file. Will default to what is in appSettings.settingsFile
        /// in the application configuration file.</param>
        /// <param name="console"></param>
        public ApplicationService(
            string settingsFile = null,
            bool console = false)
        {
            this.Console = console;
            this.Logger = new SystemLogger("ChefApp");
            this.SettingsFile = settingsFile ?? System.Configuration.ConfigurationManager.AppSettings["settingsFile"];
            this.Logger.LogInfo(false, "Chef service instantiated with settings file: {0}", settingsFile);
        }

        /// <summary>
        /// Start the monitoring loop. This is NON blocking.
        /// </summary>
        public void Start()
        {
            if (this.LoopThread != null)
            {
                this.Logger.LogInfo(false, "Chef service already started. Canno start again.");
                throw new Exception("Service loop already started.");
            }

            this.LoopThread = new Thread(() =>
            {
                this.Loop();
            });

            this.LoopThread.Start();

            this.Logger.LogInfo(false, "Chef service loop started with loop frequency {0}ms", this.Sleep);
        }

        /// <summary>
        /// Blocking waitPauseMs for the service to stop.
        /// </summary>
        public void Stop()
        {
            this.StopSignal = true;

            var start = DateTime.UtcNow;

            while (!this.Stopped)
            {
                Thread.Sleep(500);

                if ((DateTime.UtcNow - start).TotalSeconds > 60)
                {
                    this.Logger.LogInfo(false, "Chef service took more than 60 seconds and could not be stopped.");
                    break;
                }
            }

            // The thread is still running...
            if (!this.Stopped)
            {
                try
                {
                    this.LoopThread.Abort();
                }
                catch (Exception e)
                {
                    this.Logger.LogException(e);
                }

                this.Logger.LogInfo(false, "Chef service loop stopped forcefully.");
            }
        }

        protected void Loop()
        {
            while (true)
            {
                // Something breaking the loop could be very damagging
                // so wrap EVERYTHING in a try-catch loop
                try
                {
                    if (this.StopSignal)
                    {
                        // Flag as stopped and break
                        // the loop.
                        this.Stopped = true;
                        return;
                    }

                    this.LoopImplementation();

                    if (this.StopSignal)
                    {
                        // Flag as stopped and break
                        // the loop.
                        this.Stopped = true;
                        return;
                    }
                }
                catch (Exception e)
                {
                    this.Logger.LogException(
                        new Exception("Unhandled exception in loop", e));
                }
                finally
                {
                    Thread.Sleep(this.Sleep);
                }
            }
        }

        /// <summary>
        /// What a loop really does
        /// </summary>
        protected void LoopImplementation()
        {
            // This ensures that settings and other
            // stuff is reloaded on every loop.
            BindingRedirectHandler.DoBindingRedirects(AppDomain.CurrentDomain);

            var app = this.Console ? ConsoleUtils.GetApplicationForConsole() : new Application(this.Logger);

            app.Initialize(this.SettingsFile);

            app.RunServiceLoop();

            app.RunDeploymentLoop();
        }
    }
}
