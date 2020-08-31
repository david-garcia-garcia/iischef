using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iischef.core.IIS
{
    /// <summary>
    /// Replaces the enum in Sistem.Web.Administration.StartMode
    /// because we cannot use that namespace.
    /// </summary>
    public enum StartMode
    {
        OnDemand = 0,
        AlwaysRunning = 1
    }
}
