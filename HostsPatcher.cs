using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace StarkFNLauncher
{
    public static class HostsPatcher
    {
        private const string HOSTS  = @"C:\Windows\System32\drivers\etc\hosts";
        private const string MARKER = "# starkfn_entry";
        private static readonly string[] EPIC_HOSTS = {
            "account-public-service-prod.ol.epicgames.com",
            "account-public-service-prod03.ol.epicgames.com",
            "fortnite-public-service-prod11.ol.epicgames.com",
            "fortnitewaitingroom.ol.epicgames.com",
            "datarouter.ol.epicgames.com",
            "tracking.epicgames.com",
            "lightswitch-public-service-prod06.ol.epicgames.com",
            "channels-public-service-prod.ol.epicgames.com",
            "friends-public-service-prod07.ol.epicgames.com",
            "presence-public-service-prod.ol.epicgames.com",
            "fngw-prod-cdn-epicgames.ol.epicgames.com",
        };

        public static async Task<bool> PatchAsync()
        {
            try
            {
                KillEpic();
                await Task.Delay(1200);
                var lines = File.Exists(HOSTS)
                    ? File.ReadAllLines(HOSTS).Where(l => !l.Contains(MARKER)).ToList()
                    : new List<string>();
                while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
                    lines.RemoveAt(lines.Count - 1);
                lines.Add("");
                lines.Add($"# Stark FN {MARKER}");
                foreach (var h in EPIC_HOSTS)
                    lines.Add($"127.0.0.1 {h} {MARKER}");
                File.WriteAllLines(HOSTS, lines);
                Process.Start(new ProcessStartInfo { FileName = "cmd.exe", Arguments = "/c ipconfig /flushdns", UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden })?.WaitForExit(5000);
                return true;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Hosts patch failed:\n{ex.Message}\n\nRun as Administrator.", "Stark FN", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return false;
            }
        }

        public static void RestoreHosts()
        {
            try
            {
                if (!File.Exists(HOSTS)) return;
                var lines = File.ReadAllLines(HOSTS).Where(l => !l.Contains(MARKER)).ToList();
                while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1])) lines.RemoveAt(lines.Count - 1);
                File.WriteAllLines(HOSTS, lines);
                Process.Start(new ProcessStartInfo { FileName = "cmd.exe", Arguments = "/c ipconfig /flushdns", UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden })?.WaitForExit(3000);
            }
            catch { }
        }

        public static bool IsPatched()
        {
            try { return File.Exists(HOSTS) && File.ReadAllText(HOSTS).Contains(MARKER); }
            catch { return false; }
        }

        public static void KillEpic()
        {
            foreach (var name in new[] { "EpicGamesLauncher", "EpicWebHelper", "EpicGames" })
                foreach (var p in Process.GetProcessesByName(name))
                    try { p.Kill(); p.WaitForExit(3000); } catch { }
        }
    }
}
