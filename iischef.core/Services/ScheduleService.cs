using iischef.utils;
using Microsoft.Win32.TaskScheduler;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace iischef.core.Services
{
    public class ScheduleService : DeployerBase, IDeployerInterface
    {
        /// <summary>
        /// Folder in the scheduler where all the tasks will be stored
        /// </summary>
        protected const string CST_TASK_FOLDER_NAME = "\\Chef";

        /// <summary>
        /// Service typed settings
        /// </summary>
        protected ScheduleServiceSettings Settings => this.DeployerSettings.castTo<ScheduleServiceSettings>();

        /// <inheritdoc cref="DeployerInterface"/>
        public void start()
        {
            using (TaskService ts = new TaskService())
            {
                var task = this.GetTask(ts);
                task.Enabled = this.Settings.disabled != true;
            }
        }

        /// <inheritdoc cref="DeployerInterface"/>
        public void stop()
        {
            using (TaskService ts = new TaskService())
            {
                var task = this.GetTask(ts);

                if (task == null)
                {
                    this.Logger.LogWarning(false, "Could not find scheduler task {0}", this.GetTaskName(ts));
                    return;
                }

                this.DisableAndStopTask(task);
            }
        }

        /// <summary>
        /// Get the name used for the scheduled task
        /// </summary>
        /// <returns></returns>
        protected string GetTaskName(TaskService ts)
        {
            var f = this.GetFolder(ts);
            return f.Path + "\\" + this.CronId();
        }

        /// <summary>
        /// Get the instance of the task
        /// </summary>
        /// <param name="ts"></param>
        /// <returns></returns>
        protected Task GetTask(TaskService ts)
        {
            return ts.GetTask(this.GetTaskName(ts));
        }

        /// <summary>
        /// Get the cron ID for this service
        /// </summary>
        /// <returns></returns>
        protected string CronId()
        {
            var settings = this.DeployerSettings.castTo<ScheduleServiceSettings>();
            return this.Deployment.shortid + "_" + settings.id; 
        }

        public void deploySettings(
            string jsonSettings,
            string jsonSettingsNested,
            RuntimeSettingsReplacer replacer)
        {
        }

        /// <inheritdoc cref="DeployerInterface"/>
        public void undeploy(bool isUninstall = false)
        {
            using (TaskService ts = new TaskService())
            {
                var task = this.GetTask(ts);

                if (task == null)
                {
                    this.Logger.LogWarning(true, "Could not find scheduler task {0}", this.GetTaskName(ts));
                    return;
                }

                this.DisableAndStopTask(task);
                var f = this.GetFolder(ts);
                f.DeleteTask(task.Name, false);
            }
        }

        protected TaskFolder GetFolder(TaskService ts)
        {
            TaskFolder f = null;

            try
            {
                f = ts.GetFolder(CST_TASK_FOLDER_NAME);
            }
            catch
            {
                // ignored
            }

            return f ?? ts.RootFolder.CreateFolder(CST_TASK_FOLDER_NAME);
        }

        /// <summary>
        /// Waits for a task to stop running (must be in disabled state)
        /// </summary>
        /// <param name="t"></param>
        /// <param name="maxWaitMilliseconds"></param>
        protected void DisableAndStopTask(Task t, int maxWaitMilliseconds = 30000)
        {
            this.Logger.LogInfo(true, "Stopping scheduler task {0} with state {1}", t.Name, t.State);

            t.Enabled = false;

            bool hasStopped = UtilsSystem.WaitWhile(
            () => t.State == TaskState.Running,
            maxWaitMilliseconds,
            $"Waiting for task {t.Name} to stop running...",
            this.Logger);

            // The task did not stop by itself, so we need to forcefully close it.
            if (!hasStopped)
            {
                this.Logger.LogInfo(true, "Forcefully stopping task {0} ", t.Name);
                t.Stop();
            }

            // Wait again
            hasStopped = UtilsSystem.WaitWhile(
                () => t.State == TaskState.Running,
                maxWaitMilliseconds,
                $"Waiting for task {t.Name} to stop...",
                this.Logger);

            if (!hasStopped)
            {
                this.Logger.LogWarning(false, "Could not stop scheduled task {0}", t.Name);
            }
        }

        /// <inheritdoc cref="DeployerInterface"/>
        public void deploy()
        {
            var settings = this.Settings;

            string cronId = this.Deployment.shortid + "_" + settings.id;

            string pwfile = UtilsSystem.CombinePaths(this.Deployment.runtimePath, "cronjobs_" + settings.id + ".ps1");

            // Necesitamos un Bat que llame al powershel, este siempre tiene el mismo aspecto.
            string batfile = UtilsSystem.CombinePaths(this.Deployment.runtimePath, "cronjobs_" + settings.id + ".bat");

            Encoding enc = Encoding.GetEncoding("Windows-1252");
            File.WriteAllText(batfile, "powershell " + pwfile, enc);

            StringBuilder command = new StringBuilder();

            // Add path to environment.
            command.AppendLine(
                $"$env:Path = \"{UtilsSystem.CombinePaths(this.Deployment.runtimePath, "include_path")};\" + $env:Path");

            // Move to runtime.
            command.AppendLine($"cd \"{UtilsSystem.CombinePaths(this.Deployment.appPath)}\"");

            // Add path of project to the enviroment
            command.AppendLine($"$env:AppPath = \"{UtilsSystem.CombinePaths(this.Deployment.appPath)}\"");

            // Whatever deployers wanna do...
            var logger = new logger.NullLogger();
            var deployers = this.Deployment.GrabDeployers(logger);

            foreach (var deployer in deployers)
            {
                deployer.deployConsoleEnvironment(command);
            }

            // Drop the user commands
            if (!string.IsNullOrWhiteSpace(settings.command))
            {
                command.AppendLine(settings.command);
            }

            if (settings.commands != null)
            {
                foreach (var cmd in settings.commands)
                {
                    command.AppendLine(cmd);
                }
            }

            File.WriteAllText(pwfile, command.ToString());

            // Nuestro scheduler tiene un nombre
            // definido.
            using (TaskService ts = new TaskService())
            {
                // Create a new task definition and assign properties
                TaskDefinition td = ts.NewTask();

                // Run with highest level to avoid UAC issues
                // https://www.devopsonwindows.com/create-scheduled-task/
                td.Principal.RunLevel = TaskRunLevel.Highest;

                string password = settings.taskUserPassword;

                if (settings.taskLogonType.HasValue)
                {
                    td.Principal.LogonType = (TaskLogonType)settings.taskLogonType.Value;
                }

                if (settings.taskUserId == "auto")
                {
                    td.Principal.UserId = this.Deployment.WindowsUsernameFqdn();
                    td.Principal.LogonType = TaskLogonType.Password;
                    password = this.Deployment.GetWindowsPassword();

                    // Make sure that the user has the LogonAsBatchRight
                    UtilsWindowsAccounts.SetRight(this.Deployment.WindowsUsernameFqdn(), "SeBatchLogonRight", logger);
                }
                
                // Default to the SYSTEM account.
                else if (string.IsNullOrWhiteSpace(settings.taskUserId))
                {
                    td.Principal.UserId = "SYSTEM";
                    td.Principal.LogonType = TaskLogonType.ServiceAccount;
                    password = null;
                }

                td.RegistrationInfo.Description = cronId;

                // Create a trigger that will fire the task every 5 minutes.
                var trigger = new DailyTrigger();

                // Habilitada...
                trigger.Enabled = true;

                // Repetir cada 24 horas.
                trigger.DaysInterval = 1;

                // Repetir durante 24 horas en la frecuencia establecida.
                trigger.Repetition = new RepetitionPattern(new TimeSpan(0, settings.frequency, 0), new TimeSpan(24, 0, 0), true);

                // Para que arranque dos minutos después del deploy.
                trigger.StartBoundary = DateTime.Now.AddMinutes(2);

                // Enablin/disabling will happen during start/stop of service
                td.Settings.Enabled = false;

                // Un solo trigger.
                td.Triggers.Add(trigger);

                // Create an action that will launch the bat launcher.
                td.Actions.Add(new ExecAction(batfile, null, null));

                TaskFolder f = this.GetFolder(ts);

                // Register the task in the root folder
                if (!string.IsNullOrWhiteSpace(password) && td.Principal.LogonType == TaskLogonType.Password)
                {
                    f.RegisterTaskDefinition(td.RegistrationInfo.Description, td, TaskCreation.Create, td.Principal.UserId, this.Deployment.GetWindowsPassword(), td.Principal.LogonType);
                }
                else
                {
                    f.RegisterTaskDefinition(td.RegistrationInfo.Description, td, TaskCreation.Create, td.Principal.UserId);
                }
            }
        }

        /// <inheritdoc cref="DeployerInterface"/>
        public override void cleanup()
        {
            using (TaskService ts = new TaskService())
            {
                var f = this.GetFolder(ts);

                foreach (var t in f.AllTasks.ToList())
                {
                    // If task does not belong to our app Skip
                    if (!t.Name.StartsWith(this.Deployment.GetShortIdPrefix()))
                    {
                        continue;
                    }

                    // If task is from this deployment, Skip
                    if (t.Name.StartsWith(this.Deployment.shortid))
                    {
                        continue;
                    }

                    this.Logger.LogWarning(true, "Removed stuck scheduler task {0}", t.Name);
                    f.DeleteTask(t.Name);
                }
            }
        }

        /// <inheritdoc cref="DeployerInterface"/>
        public void sync()
        {
        }
    }
}
