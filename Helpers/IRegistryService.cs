namespace Screenshoter
{
    public interface IRegistryService
    {
        string? GetValue(string subKey, string valueName);
        void SetValue(string subKey, string valueName, string? value);
        void DeleteValue(string subKey, string valueName, bool throwOnMissing);
    }
}
