using Microsoft.Win32;

namespace Screenshoter
{
    internal sealed class WindowsRegistryService : IRegistryService
    {
        public string? GetValue(string subKey, string valueName)
        {
            using var key = Registry.CurrentUser.OpenSubKey(subKey, false);
            return key?.GetValue(valueName) as string;
        }

        public void SetValue(string subKey, string valueName, string? value)
        {
            using var key = Registry.CurrentUser.OpenSubKey(subKey, true)
                         ?? Registry.CurrentUser.CreateSubKey(subKey);
            if (key != null) key.SetValue(valueName, value ?? string.Empty);
        }

        public void DeleteValue(string subKey, string valueName, bool throwOnMissing)
        {
            using var key = Registry.CurrentUser.OpenSubKey(subKey, true);
            key?.DeleteValue(valueName, throwOnMissing);
        }
    }
}
