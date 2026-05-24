using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace StarkFNLauncher
{
    public enum LaunchState { Idle, Launching, Running, Failed }

    public static class GameLauncher
    {
        public static LaunchState State   { get; private set; } = LaunchState.Idle;
        public static string      Error   { get; private set; } = "";
        public static int         GamePid { get; private set; }

        private static int _seq = 0;

        public static void Launch(string buildPath, string username, string accountId, string authToken)
        {
            if (State != LaunchState.Idle) return;
            State = LaunchState.Launching;
            Error = "";

            int seq = Interlocked.Increment(ref _seq);
            Task.Run(async () =>
            {
                try { await DoLaunch(buildPath, username, authToken, seq); }
                catch (Exception ex) { Error = ex.Message; State = LaunchState.Idle; Toast($"Error: {ex.Message}"); }
            });
        }

        public static void Stop()
        {
            Interlocked.Increment(ref _seq);
            foreach (var name in new[] { "FortniteClient-Win64-Shipping", "FortniteLauncher", "FortniteClient-Win64-Shipping_EAC", "FortniteClient-Win64-Shipping_EAC_EOS" })
                foreach (var p in Process.GetProcessesByName(name))
                    try { p.Kill(); } catch { }
            State   = LaunchState.Idle;
            GamePid = 0;
        }

        private static async Task DoLaunch(string buildPath, string username, string authToken, int seq)
        {
            // Find the game exe
            string? gameExe = FindExe(buildPath, "FortniteClient-Win64-Shipping.exe");
            if (string.IsNullOrEmpty(gameExe))
            {
                Toast("FortniteClient-Win64-Shipping.exe not found.");
                State = LaunchState.Idle;
                return;
            }

            string gameDir = Path.GetDirectoryName(gameExe)!;

            // Kill EAC processes so they don't error
            foreach (var name in new[] { "FortniteClient-Win64-Shipping_EAC", "FortniteClient-Win64-Shipping_EAC_EOS", "FortniteLauncher" })
                foreach (var p in Process.GetProcessesByName(name))
                    try { p.Kill(); } catch { }

            await Task.Delay(500);
            if (_seq != seq) return;

            // Build auth
            string safeToken = authToken.Length >= 8 ? authToken[..8] : authToken;
            string login     = $"{username}@starkfn.gg";
            string password  = $"STARK FN_{safeToken}";

            Toast("Launching Fortnite 14.60...");

            var psi = new ProcessStartInfo
            {
                FileName         = gameExe,
                WorkingDirectory = gameDir,
                UseShellExecute  = false,
            };

            // Core args
            psi.ArgumentList.Add("-epicapp=Fortnite");
            psi.ArgumentList.Add("-epicenv=Prod");
            psi.ArgumentList.Add("-epiclocale=en-us");
            psi.ArgumentList.Add("-epicportal");

            // Skip ALL update/patch checks
            psi.ArgumentList.Add("-skippatchcheck");
            psi.ArgumentList.Add("-skipdatapatchcheck");
            psi.ArgumentList.Add("-skipstartupmoviecheckslowload");

            // Disable ALL anticheat — fixes EAC error
            psi.ArgumentList.Add("-noeac");
            psi.ArgumentList.Add("-nobe");
            psi.ArgumentList.Add("-nouac");
            psi.ArgumentList.Add("-nocodeguards");
            psi.ArgumentList.Add("-notexturestreaming");

            // Auth
            psi.ArgumentList.Add($"-AUTH_LOGIN={login}");
            psi.ArgumentList.Add($"-AUTH_PASSWORD={password}");
            psi.ArgumentList.Add("-AUTH_TYPE=epic");

            // Season / misc
            psi.ArgumentList.Add("-starkfnseason=C2S4");
            psi.ArgumentList.Add("-dx11");
            psi.ArgumentList.Add("-nosplash");

            Process? proc = null;
            try { proc = Process.Start(psi); }
            catch (Exception ex)
            {
                Toast($"Launch failed: {ex.Message}");
                State = LaunchState.Idle;
                return;
            }

            if (proc == null)
            {
                Toast("Failed to start Fortnite.");
                State = LaunchState.Idle;
                return;
            }

            GamePid = proc.Id;
            State   = LaunchState.Running;
            Toast("Fortnite 14.60 launched!");

            await Task.Run(() => proc.WaitForExit());

            if (_seq == seq) { GamePid = 0; State = LaunchState.Idle; }
        }

        private static string? FindExe(string buildPath, string exeName)
        {
            string[] candidates = {
                Path.Combine(buildPath, exeName),
                Path.Combine(buildPath, "FortniteGame", "Binaries", "Win64", exeName),
                Path.Combine(buildPath, "Binaries", "Win64", exeName),
                Path.Combine(buildPath, "Win64", exeName),
            };
            foreach (var c in candidates)
                if (File.Exists(c)) return c;
            try
            {
                foreach (var f in Directory.GetFiles(buildPath, exeName, SearchOption.AllDirectories))
                    return f;
            }
            catch { }
            return null;
        }

        private static void Toast(string msg)
            => System.Windows.Application.Current?.Dispatcher.Invoke(
                () => ToastManager.Show(msg));
    }

    public static class ToastManager
    {
        public static Action<string>? OnToast;
        public static void Show(string msg) => OnToast?.Invoke(msg);
    }
}
