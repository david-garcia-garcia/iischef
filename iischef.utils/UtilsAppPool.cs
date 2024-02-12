using iischef.logger;
using Microsoft.Web.Administration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace iischef.utils
{
    /// <summary>
    /// Util class to deal with application pool / site management
    /// </summary>
    public class UtilsAppPool
    {
        /// <summary>
        /// The number of milliseconds to wait in looped pauses
        /// </summary>
        protected const int WaitPauseMs = 90;

        /// <summary>
        /// The maximum number of milliseconds to wait
        /// for an operaiton to complete
        /// </summary>
        protected const int WaitMaxForProcessMs = 5000;

        /// <summary>
        /// The logger
        /// </summary>
        protected ILoggerInterface Logger { get; set; }

        /// <summary>
        /// Get an intance of UtilsAppPool
        /// </summary>
        /// <param name="logger"></param>
        public UtilsAppPool(ILoggerInterface logger)
        {
            this.Logger = logger;
        }

        /// <summary>
        /// Stop an application pool
        /// </summary>
        /// <param name="p"></param>
        /// <param name="maxWait"></param>
        /// <returns></returns>
        private bool StopAppPool(ApplicationPool p, int maxWait = WaitMaxForProcessMs)
        {
            ObjectState state = ObjectState.Unknown;

            UtilsSystem.RetryWhile(
                () =>
                {
                    state = p.State;
                    return true;
                },
                (e) => e is COMException,
                3000,
                this.Logger);

            if (state == ObjectState.Stopped)
            {
                return true;
            }

            if (state != ObjectState.Started)
            {
                return false;
            }

            p.Stop();

            Stopwatch sw = Stopwatch.StartNew();
            sw.Start();

            while (true)
            {
                if (sw.ElapsedMilliseconds > maxWait)
                {
                    break;
                }

                Thread.Sleep(WaitPauseMs);

                if (p.State == ObjectState.Stopped)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Start an application pool
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        private bool StartAppPool(ApplicationPool p)
        {
            ObjectState state = ObjectState.Unknown;

            UtilsSystem.RetryWhile(
                () =>
                {
                    state = p.State;
                    return true;
                },
                (e) => e is COMException,
                3000,
                this.Logger);

            // Do nothing if already started
            if (state == ObjectState.Started)
            {
                return true;
            }

            // If not started, but not in a stopped state,
            // the pool is in a transient state (stopping, starting)
            // and nothing should be done
            if (state != ObjectState.Stopped)
            {
                return false;
            }

            p.Start();

            Stopwatch sw = Stopwatch.StartNew();
            sw.Start();

            while (true)
            {
                if (sw.ElapsedMilliseconds > WaitMaxForProcessMs)
                {
                    break;
                }

                Thread.Sleep(WaitPauseMs);

                if (p.State == ObjectState.Started)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Stop a site
        /// </summary>
        /// <param name="p"></param>
        /// <param name="maxWait"></param>
        /// <returns></returns>
        private bool StopSite(Site p, int maxWait = WaitMaxForProcessMs)
        {
            ObjectState state = ObjectState.Unknown;

            UtilsSystem.RetryWhile(
                () =>
                {
                    state = p.State;
                    return true;
                },
                (e) => e is COMException,
                3000,
                this.Logger);

            if (state == ObjectState.Stopped)
            {
                return true;
            }

            if (state != ObjectState.Started)
            {
                return false;
            }

            UtilsSystem.RetryWhile(() => p.Stop(), (e) => true, 2000, this.Logger);

            Stopwatch sw = Stopwatch.StartNew();
            sw.Start();

            while (true)
            {
                if (sw.ElapsedMilliseconds > maxWait)
                {
                    break;
                }

                if (p.State == ObjectState.Stopped)
                {
                    return true;
                }

                Thread.Sleep(WaitPauseMs);
            }

            return false;
        }

        /// <summary>
        /// Start a site
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        private bool StartSite(Site p)
        {
            ObjectState state = ObjectState.Unknown;

            UtilsSystem.RetryWhile(
                () =>
                {
                    state = p.State;
                    return true;
                },
                (e) => e is COMException,
                3000,
                this.Logger);

            if (state == ObjectState.Started)
            {
                return true;
            }

            if (state != ObjectState.Stopped)
            {
                return false;
            }

            try
            {
                // Try a couple of times before actually handling an exception
                UtilsSystem.RetryWhile(
                    () => p.Start(),
                    (e) =>
                    {
                        if (e is ServerManagerException serverManagerException)
                        {
                            // Caso de que hay un conflicto de bindings, hay que encontrar cual es para ayudar
                            // al admin.
                            if (serverManagerException.ErrorCode == 102)
                            {
                                var bindings = UtilsIis.ParseSiteBindings(this.Logger);

                                var siteBindings = bindings.Where((i) => i.Sites.Any((j) => j.Name == p.Name)).ToList();

                                foreach (var binding in siteBindings)
                                {
                                    var startedSite = binding.Sites.FirstOrDefault((i) => i.State == ObjectState.Started);
                                    if (startedSite != null)
                                    {
                                        this.Logger.LogWarning(false, $"Binding '{binding.Binding.Binding}' from site '{p.Name}' already in use at site '{startedSite.Name}'");
                                    }
                                }

                                return false;
                            }
                        }

                        // Caso de que el puerto está en uso
                        if (e is FileLoadException fileLoadException)
                        {
                            List<string> ports = new List<string>();

                            foreach (var binding in p.Bindings)
                            {
                                if (ports.Contains(binding.EndPoint.Port.ToString()))
                                {
                                    continue;
                                }

                                ports.Add(binding.EndPoint.Port.ToString());
                            }

                            // Let's give a hint into what ports might be in use....
                            var bindings = (from binding in p.Bindings
                                            select binding.Host + "@" + Convert.ToString(binding.EndPoint));

                            // Try to figure out what process is using the port...
                            string process = null;

                            try
                            {
                                var processes = UtilsProcessPort.GetNetStatPorts();
                                process = string.Join(
                                    ", " + Environment.NewLine,
                                    processes.Where((i) => ports.Contains(i.port_number))
                                        .Select((i) => $"{i.process_name}:{i.port_number}"));
                            }
                            catch
                            {
                                // ignored
                            }

                            throw new Exception(
                                                "Cannot start website, a port or binding might be already in use by another application or website: " + Environment.NewLine
                                                + string.Join(", " + Environment.NewLine, bindings) + Environment.NewLine
                                                + "The following port usages have been detected:" + Environment.NewLine
                                                + process, e);
                        }

                        return true;
                    },
                    3000,
                    this.Logger);
            }
            catch (Exception e)
            {
                if (Convert.ToString(e.HResult) == "-2146233088")
                {
                    this.Logger.LogError("Cannot start website: " +
                                               Environment.NewLine +
                                               e.Message +
                                               Environment.NewLine +
                                               e.InnerException?.Message);
                    return true;
                }
                else
                {
                    throw;
                }
            }

            Stopwatch sw = Stopwatch.StartNew();
            sw.Start();

            while (true)
            {
                if (sw.ElapsedMilliseconds > WaitMaxForProcessMs)
                {
                    break;
                }

                if (p.State == ObjectState.Started)
                {
                    return true;
                }

                Thread.Sleep(WaitPauseMs);
            }

            return false;
        }

        /// <summary>
        /// Changes the state of a site (start/stop/reset) including it's application pools.
        /// </summary>
        /// <param name="sitename">Name of the site</param>
        /// <param name="action">Action to run on the site</param>
        /// <param name="skipApplicationPools">Do not action over application pools related to the iste</param>
        /// <param name="persistStateAsDefault">If we stop or start the site, set this as the default state for the site with ServerAutoStart (i.e. after reboot)</param>
        /// <returns></returns>
        public void WebsiteAction(
            string sitename,
            AppPoolActionType action,
            bool skipApplicationPools = false,
            bool persistStateAsDefault = true)
        {
            using (ServerManager manager = new ServerManager())
            {
                bool changed = false;

                // Buscamos el site....
                var site = UtilsIis
                    .FindSiteWithName(manager, sitename, this.Logger)
                    .SingleOrDefault();

                if (site == null)
                {
                    return;
                }

                // Cargamos TODOS los application pools de ese site...
                List<ApplicationPool> pools = new List<ApplicationPool>();

                if (!skipApplicationPools)
                {
                    foreach (var s in site.Applications)
                    {
                        var applicationPool =
                            manager.ApplicationPools.SingleOrDefault(i => i.Name == s.ApplicationPoolName);

                        if (applicationPool == null)
                        {
                            throw new Exception(
                                string.Format(
                                    "Could not find application pool with name '{3}' for application with path '{0}' and site '{1}' ({2}).",
                                    s.Path,
                                    site.Name,
                                    site.Id,
                                    s.ApplicationPoolName));
                        }

                        pools.Add(applicationPool);
                    }
                }

                switch (action)
                {
                    case AppPoolActionType.Start:
                        // Start all pools, then the site
                        foreach (var p in pools)
                        {
                            this.StartAppPool(p);
                        }

                        this.StartSite(site);

                        if (persistStateAsDefault && site.ServerAutoStart != true)
                        {
                            changed = true;
                            site.ServerAutoStart = true;
                        }

                        break;
                    case AppPoolActionType.Stop:

                        // Stop site, then pools
                        this.StopSite(site, 10000);

                        foreach (var p in pools)
                        {
                            this.StopAppPool(p, 10000);
                        }

                        if (persistStateAsDefault && site.ServerAutoStart != false)
                        {
                            changed = true;
                            site.ServerAutoStart = false;
                        }

                        break;
                    case AppPoolActionType.Reset:

                        // Stop site
                        this.StopSite(site, 10000);

                        // Stop pools
                        foreach (var p in pools)
                        {
                            this.StopAppPool(p, 10000);
                        }

                        // Start pools
                        foreach (var p in pools)
                        {
                            this.StartAppPool(p);
                        }

                        // Start site
                        this.StartSite(site);

                        break;
                }

                // Commit because we changed the autostart property
                if (changed)
                {
                    UtilsIis.CommitChanges(manager);
                }
            }
        }
    }
}
