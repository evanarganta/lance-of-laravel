using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace LaravelLauncher.Services
{
    public class DevServerRunner
    {
        private Process? _artisanProcess;
        private Process? _viteProcess;
        private Process? _composerDevProcess;

        public bool IsRunning => (_artisanProcess != null && !_artisanProcess.HasExited) ||
                                 (_composerDevProcess != null && !_composerDevProcess.HasExited);

        public async Task<bool> StartServerAsync(
            string projectPath,
            string targetUrl,
            bool isLaragonMode,
            Action<string>? logCallback,
            CancellationToken cancelToken = default)
        {
            StopAllServers(logCallback);

            if (isLaragonMode)
            {
                logCallback?.Invoke($"🌐 Project running in Laragon Virtual Host mode ({targetUrl}). Apache handles PHP serving.");

                // If Vite/NPM script exists, launch npm run dev for hot-reloading asset bundling
                if (File.Exists(Path.Combine(projectPath, "package.json")))
                {
                    logCallback?.Invoke("⚡ Starting Vite dev server for frontend asset updates ('npm run dev')...");
                    _viteProcess = StartBackgroundProcess("npm", "run dev", projectPath, logCallback, "VITE");
                }

                return await PollAndOpenBrowserAsync(targetUrl, logCallback, cancelToken);
            }

            // Mode A: Localhost:8000
            logCallback?.Invoke("🚀 Launching Laravel Development Server (localhost:8000)...");
            logCallback?.Invoke("⚡ Running 'php artisan serve --port=8000'...");
            _artisanProcess = StartBackgroundProcess("php", "artisan serve --port=8000", projectPath, logCallback, "ARTISAN");

            if (File.Exists(Path.Combine(projectPath, "package.json")))
            {
                logCallback?.Invoke("⚡ Starting Vite dev server ('npm run dev')...");
                _viteProcess = StartBackgroundProcess("npm", "run dev", projectPath, logCallback, "VITE");
            }

            return await PollAndOpenBrowserAsync(targetUrl, logCallback, cancelToken);
        }

        private bool CheckComposerDevScript(string projectPath)
        {
            try
            {
                string composerJsonPath = Path.Combine(projectPath, "composer.json");
                if (File.Exists(composerJsonPath))
                {
                    string content = File.ReadAllText(composerJsonPath);
                    return content.Contains("\"dev\":");
                }
            }
            catch { }
            return false;
        }

        private Process StartBackgroundProcess(string fileName, string args, string workingDir, Action<string>? logCallback, string tag)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c {fileName} {args}",
                WorkingDirectory = workingDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var proc = new Process { StartInfo = psi };
            proc.OutputDataReceived += (s, e) => { if (e.Data != null) logCallback?.Invoke($"  [{tag}] {e.Data}"); };
            proc.ErrorDataReceived += (s, e) => { if (e.Data != null) logCallback?.Invoke($"  [{tag} ERR] {e.Data}"); };

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            return proc;
        }

        private async Task<bool> PollAndOpenBrowserAsync(string targetUrl, Action<string>? logCallback, CancellationToken cancelToken)
        {
            logCallback?.Invoke($"⌛ Waiting for server response at {targetUrl}...");
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };

            var startTime = DateTime.UtcNow;
            var timeout = TimeSpan.FromSeconds(25);
            bool responded = false;

            while (DateTime.UtcNow - startTime < timeout)
            {
                if (cancelToken.IsCancellationRequested) return false;

                try
                {
                    var response = await httpClient.GetAsync(targetUrl, cancelToken);
                    if (response.IsSuccessStatusCode || ((int)response.StatusCode >= 200 && (int)response.StatusCode < 500))
                    {
                        responded = true;
                        break;
                    }
                }
                catch
                {
                    // Server still booting up
                }

                await Task.Delay(800, cancelToken);
            }

            if (responded)
            {
                logCallback?.Invoke($"🎉 Server is UP and READY! Opening browser automatically at {targetUrl}...");
            }
            else
            {
                logCallback?.Invoke($"⚠️ Timeout waiting for {targetUrl}. Opening browser anyway...");
            }

            OpenBrowser(targetUrl, logCallback);
            return true;
        }

        private void OpenBrowser(string url, Action<string>? logCallback)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                logCallback?.Invoke($"⚠️ Failed to open default browser: {ex.Message}");
            }
        }

        public void StopAllServers(Action<string>? logCallback = null)
        {
            KillProcess(_artisanProcess, "Artisan Serve", logCallback);
            KillProcess(_viteProcess, "Vite Dev Server", logCallback);
            KillProcess(_composerDevProcess, "Composer Dev Server", logCallback);

            _artisanProcess = null;
            _viteProcess = null;
            _composerDevProcess = null;
        }

        private void KillProcess(Process? proc, string name, Action<string>? logCallback)
        {
            if (proc != null && !proc.HasExited)
            {
                try
                {
                    logCallback?.Invoke($"⏹ Stopping {name}...");
                    proc.Kill(entireProcessTree: true);
                }
                catch { }
            }
        }
    }
}
