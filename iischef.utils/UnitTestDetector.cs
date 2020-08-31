using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace iischef.utils
{
    /// <summary>
    /// Detect if we are running as part of a xUnit unit test.
    /// This is DIRTY and should only be used if absolutely necessary 
    /// as its usually a sign of bad design.
    /// </summary>    
    public static class UnitTestDetector
    {
        /// <summary>
        /// 
        /// </summary>
        private static bool runningInTests = false;

        /// <summary>
        /// 
        /// </summary>
        static UnitTestDetector()
        {
            foreach (Assembly assem in AppDomain.CurrentDomain.GetAssemblies())
            {
                string testAssemblyName = "xunit.runner";
                runningInTests = AppDomain.CurrentDomain.GetAssemblies()
                    .Any(a => a.FullName.StartsWith(testAssemblyName));
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public static bool IsRunningInTests
        {
            get { return runningInTests; }
        }
    }
}
