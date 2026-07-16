using System;
using System.Diagnostics;

namespace Screenshoter
{
    public static class Autostart
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "SnapFlow";

        internal static IRegistryService? RegistryService { private get; set; }

        private static string ExePath =>
            Process.GetCurrentProcess().MainModule?.FileName
            ?? Environment.ProcessPath ?? "";

        public static bool IsEnabled()
        {
            try
            {
                var reg = RegistryService ?? new WindowsRegistryService();
                var val = reg.GetValue(RunKey, ValueName);
                return !string.IsNullOrEmpty(val);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Autostart.IsEnabled failed");
                return false;
            }
        }

        public static void SetEnabled(bool enabled)
        {
            try
            {
                var reg = RegistryService ?? new WindowsRegistryService();
                if (enabled)
                    reg.SetValue(RunKey, ValueName, $"\"{ExePath}\"");
                else
                    reg.DeleteValue(RunKey, ValueName, false);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Autostart.SetEnabled failed");
            }
        }
    }
}
