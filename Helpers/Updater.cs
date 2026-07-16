using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Screenshoter
{
    public static class Updater
    {
        private const string Owner = "Onzis";
        private const string Repo = "SnapFlow";
        private const string ApiUrl =
            "https://api.github.com/repos/" + Owner + "/" + Repo + "/releases/latest";

        public sealed class ReleaseInfo
        {
            public Version Version { get; init; } = new(0, 0, 0);
            public string Tag { get; init; } = "";
            public string Name { get; init; } = "";
            public string Notes { get; init; } = "";
            public string DownloadUrl { get; init; } = "";
            public string HtmlUrl { get; init; } = "";
            public string? ExpectedSha256 { get; init; }
        }

        public static Version CurrentVersion =>
            Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);

        private static HttpClient CreateClient()
        {
            var c = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            c.DefaultRequestHeaders.UserAgent.ParseAdd("SnapFlow-Updater");
            c.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            return c;
        }

        public static async Task<ReleaseInfo?> GetLatestAsync()
        {
            try
            {
                using var client = CreateClient();
                var json = await client.GetStringAsync(ApiUrl);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string tag = root.GetProperty("tag_name").GetString() ?? "";
                string html = root.TryGetProperty("html_url", out var h) ? h.GetString() ?? "" : "";
                string name = root.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                string notes = root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";

                string url = "";
                if (root.TryGetProperty("assets", out var assets))
                {
                    foreach (var a in assets.EnumerateArray())
                    {
                        var assetName = a.GetProperty("name").GetString() ?? "";
                        if (assetName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        {
                            url = a.GetProperty("browser_download_url").GetString() ?? "";
                            break;
                        }
                    }
                }

                string? expectedSha256 = null;
                var shaMatch = Regex.Match(notes,
                    @"(?:SHA-?256|Checksum)[:\s]+([0-9a-fA-F]{64})",
                    RegexOptions.IgnoreCase);
                if (shaMatch.Success)
                    expectedSha256 = shaMatch.Groups[1].Value.ToUpperInvariant();

                return new ReleaseInfo
                {
                    Version = ParseVersion(tag),
                    Tag = tag,
                    Name = name,
                    Notes = notes,
                    DownloadUrl = url,
                    HtmlUrl = html,
                    ExpectedSha256 = expectedSha256
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Updater.GetLatestAsync failed");
                return null;
            }
        }

        public static bool IsNewer(ReleaseInfo release) => release.Version > CurrentVersion;

        public static async Task<bool> DownloadAndApplyAsync(ReleaseInfo release)
        {
            if (string.IsNullOrEmpty(release.DownloadUrl)) return false;

            string currentExe = Process.GetCurrentProcess().MainModule?.FileName
                                ?? Environment.ProcessPath ?? "";
            if (string.IsNullOrEmpty(currentExe)) return false;

            string newExe = Path.Combine(Path.GetTempPath(),
                $"SnapFlow_{release.Version}.exe");

            using (var client = CreateClient())
            using (var resp = await client.GetAsync(release.DownloadUrl,
                       HttpCompletionOption.ResponseHeadersRead))
            {
                resp.EnsureSuccessStatusCode();
                await using var fs = new FileStream(newExe, FileMode.Create, FileAccess.Write);
                await resp.Content.CopyToAsync(fs);
            }

            if (!string.IsNullOrEmpty(release.ExpectedSha256))
            {
                var sha256 = System.Security.Cryptography.SHA256.HashData(
                    await File.ReadAllBytesAsync(newExe));
                var actualHash = Convert.ToHexString(sha256);
                if (!string.Equals(actualHash, release.ExpectedSha256, StringComparison.OrdinalIgnoreCase))
                {
                    Logger.Error(
                        $"SHA-256 mismatch for {newExe}: expected {release.ExpectedSha256}, got {actualHash}");
                    try { File.Delete(newExe); } catch { }
                    return false;
                }
                Logger.Info($"SHA-256 verified: {actualHash}");
            }
            else
            {
                Logger.Warn("No SHA-256 provided in release notes, skipping integrity check");
            }

            WriteAndRunSwapScript(currentExe, newExe);
            return true;
        }

        private static void WriteAndRunSwapScript(string currentExe, string newExe)
        {
            int pid = Process.GetCurrentProcess().Id;
            string bat = Path.Combine(Path.GetTempPath(), "snapflow_update.cmd");

            string script =
$@"@echo off
:waitloop
tasklist /FI ""PID eq {pid}"" 2>nul | find ""{pid}"" >nul
if not errorlevel 1 (
    timeout /t 1 /nobreak >nul
    goto waitloop
)
move /y ""{newExe}"" ""{currentExe}"" >nul
start """" ""{currentExe}""
del ""%~f0""
";
            File.WriteAllText(bat, script);

            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{bat}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            Process.Start(psi);
        }

        private static Version ParseVersion(string tag)
        {
            var digits = new string(tag.Where(c => char.IsDigit(c) || c == '.').ToArray());
            var parts = digits.Split('.', StringSplitOptions.RemoveEmptyEntries);
            int Major = parts.Length > 0 && int.TryParse(parts[0], out var a) ? a : 0;
            int Minor = parts.Length > 1 && int.TryParse(parts[1], out var b) ? b : 0;
            int Build = parts.Length > 2 && int.TryParse(parts[2], out var c) ? c : 0;
            return new Version(Major, Minor, Build);
        }
    }
}
