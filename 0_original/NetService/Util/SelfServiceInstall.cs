using System;
using System.Collections.Generic;
using System.Configuration.Install;
using System.Linq;
using System.Reflection;
using System.Text;

namespace NetService.Util
{
    public static class SelfServiceInstaller
    {
        public static bool Install(string exePath)
        {
            try
            {
                if (exePath == string.Empty)
                    exePath = Assembly.GetExecutingAssembly().Location;
                ManagedInstallerClass.InstallHelper(new string[] { exePath });
            }
            catch
            {
                return false;
            }
            return true;
        }

        public static bool Uninstall(string exePath)
        {
            try
            {
                ManagedInstallerClass.InstallHelper(new string[] { "/u", exePath });
            }
            catch
            {
                return false;
            }
            return true;
        }
    }
}
