using iischef.utils;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Reflection;

namespace iischef.core
{
    public class BindingRedirectHandler
    {
        protected static object Locker = new object();

        public static void DoBindingRedirects(AppDomain domain)
        {
            lock (Locker)
            {
                if (domain.GetData("bindingRedirectSetup") is bool bindingRedirectSetup && bindingRedirectSetup == true)
                {
                    return;
                }

                domain.AssemblyResolve += CurrentDomain_BindingRedirect;
                
                domain.SetData("bindingRedirectSetup", true);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        protected static Assembly CurrentDomain_BindingRedirect(object sender, ResolveEventArgs args)
        {
            var name = new AssemblyName(args.Name);

            switch (name.Name)
            {
                case "Newtonsoft.Json":
                    return typeof(JsonSerializer).Assembly;

                case "ManagedOpenSsl":
                    return LoadAssembly(Environment.Is64BitProcess ? "ManagedOpenSsl64.dll" : "ManagedOpenSsl86.dll");

                default:
                    return LoadAssembly(name.Name + ".dll", true);
            }
        }

        /// <summary>
        /// Attempt to load assemblies from different paths
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="defaultNull"></param>
        /// <returns></returns>
        protected static Assembly LoadAssembly(string fileName, bool defaultNull = false)
        {
            var assembly = LoadAssemblyWithPrefix(fileName, Environment.Is64BitProcess ? "x64" : "x86");

            if (assembly != null)
            {
                return assembly;
            }

            assembly = LoadAssemblyWithPrefix(fileName, string.Empty);

            if (assembly != null)
            {
                return assembly;
            }

            if (defaultNull)
            {
                // Null triggers defualt .Net loading mechanisms
                return null;
            }
            else
            {
                System.Console.WriteLine("Loading: " + fileName);
                return Assembly.LoadFrom(fileName);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="dir"></param>
        /// <returns></returns>
        protected static Assembly LoadAssemblyWithPrefix(string fileName, string dir)
        {
            string path;

            path = Path.Combine(UtilsSystem.GetCodeBaseDir(), dir, fileName);
            if (File.Exists(path))
            {
                return Assembly.LoadFrom(path);
            }

            path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, dir, fileName);
            if (File.Exists(path))
            {
                return Assembly.LoadFrom(path);
            }

            return null;
        }
    }
}
