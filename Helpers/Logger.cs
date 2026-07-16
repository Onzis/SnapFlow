using System;
using System.IO;

namespace Screenshoter
{
    internal static class Logger
    {
        private static readonly string LogDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SnapFlow", "logs");

        private static readonly string LogFile = Path.Combine(LogDir, "app.log");
        private static readonly object _lock = new();

        static Logger()
        {
            try { Directory.CreateDirectory(LogDir); } catch { }
        }

        public static void Info(string message) => Write("INFO", message);
        public static void Warn(string message) => Write("WARN", message);
        public static void Error(string message) => Write("ERROR", message);
        public static void Error(Exception ex, string context) =>
            Write("ERROR", $"{context}: {ex}");

        private static void Write(string level, string message)
        {
            try
            {
                lock (_lock)
                {
                    File.AppendAllText(LogFile,
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}");
                }
            }
            catch { }
        }
    }
}
