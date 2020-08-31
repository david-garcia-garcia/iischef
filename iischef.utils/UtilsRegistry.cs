using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace iischef.utils
{
    public static class UtilsRegistry
    {
        public static object GetRegistryKeyValue32(RegistryHive hive, string key, string value, object defaultValue)
        {
            var view32 = RegistryKey.OpenBaseKey(hive, RegistryView.Registry32);

            using (var clsid32 = view32.OpenSubKey(key, false))
            {
                if (clsid32 == null)
                {
                    return defaultValue;
                }

                // actually accessing Wow6432Node 
                return clsid32.GetValue(value);
            }
        }

        public static object GetRegistryKeyValue64(RegistryHive hive, string key, string value, object defaultValue)
        {
            var view64 = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);

            using (var clsid64 = view64.OpenSubKey(key, false))
            {
                if (clsid64 == null)
                {
                    return defaultValue;
                }

                // actually accessing Wow6432Node 
                return clsid64.GetValue(value);
            }
        }

        public static void SetRegistryValue(RegistryHive hive, string key, string name, object realValue, RegistryValueKind valueKind)
        {
            var view32 = RegistryKey.OpenBaseKey(hive, RegistryView.Registry32);

            using (var clsid32 = view32.OpenSubKey(key, true))
            {
                // actually accessing Wow6432Node
                if (clsid32 != null)
                {
                    clsid32.SetValue(name, realValue, valueKind);
                }
                else
                {
                    using (var k = view32.CreateSubKey(key))
                    {
                        k.SetValue(name, realValue, valueKind);
                    }
                }
            }

            var view64 = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);

            using (var clsid64 = view64.OpenSubKey(key, true))
            {
                // actually accessing Wow6432Node 
                if (clsid64 != null)
                {
                    clsid64.SetValue(name, realValue, valueKind);
                }
                else
                {
                    using (var k = view64.CreateSubKey(key))
                    {
                        k.SetValue(name, realValue, valueKind);
                    }
                }
            }
        }
    }
}
