using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Input;

namespace Screenshoter
{
    // Настройки приложения с сохранением в %AppData%\Screenshoter\settings.json
    public sealed class AppSettings
    {
        public ModifierKeys Modifiers { get; set; } = ModifierKeys.Shift | ModifierKeys.Alt;
        public Key Key { get; set; } = Key.S;

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        private static string Dir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Screenshoter");

        private static string FilePath => Path.Combine(Dir, "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var json = File.ReadAllText(FilePath);
                    var s = JsonSerializer.Deserialize<AppSettings>(json, JsonOpts);
                    if (s != null && s.Key != Key.None) return s;
                }
            }
            catch { /* повреждённый файл -> дефолты */ }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOpts));
            }
            catch { /* игнорируем ошибки записи */ }
        }

        // Перевод в модификаторы Win32
        public HotkeyManager.Mod ToWin32Mod()
        {
            HotkeyManager.Mod m = 0;
            if (Modifiers.HasFlag(ModifierKeys.Alt)) m |= HotkeyManager.Mod.Alt;
            if (Modifiers.HasFlag(ModifierKeys.Control)) m |= HotkeyManager.Mod.Control;
            if (Modifiers.HasFlag(ModifierKeys.Shift)) m |= HotkeyManager.Mod.Shift;
            if (Modifiers.HasFlag(ModifierKeys.Windows)) m |= HotkeyManager.Mod.Win;
            return m;
        }

        public uint ToVirtualKey() => (uint)KeyInterop.VirtualKeyFromKey(Key);

        // Человекочитаемое представление, напр. "Shift + Alt + S"
        public string ToDisplayString() => ToDisplayString(Modifiers, Key);

        public static string ToDisplayString(ModifierKeys mods, Key key)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (mods.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
            if (mods.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
            if (mods.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
            if (mods.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
            if (key != Key.None) parts.Add(key.ToString());
            return string.Join(" + ", parts);
        }
    }
}
