using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace LaravelLauncher.Services
{
    public class LaragonManager
    {
        public async Task<bool> EnsureLaragonServicesAsync(
            string laragonExePath,
            bool startLaragon,
            bool startApache,
            bool startMySQL,
            Action<string>? logCallback,
            CancellationToken cancelToken = default)
        {
            if (!startLaragon && !startApache && !startMySQL)
            {
                logCallback?.Invoke("ℹ️ Laragon auto-start skipped per settings.");
                return true;
            }

            string exePath = string.IsNullOrWhiteSpace(laragonExePath) ? @"C:\laragon\laragon.exe" : laragonExePath;
            string laragonDir = File.Exists(exePath) ? Path.GetDirectoryName(exePath) ?? @"C:\laragon" : @"C:\laragon";

            // Step 1: Launch Laragon main GUI & trigger reload command
            if (File.Exists(exePath))
            {
                bool isLaragonRunning = Process.GetProcessesByName("laragon").Length > 0;
                if (!isLaragonRunning && startLaragon)
                {
                    logCallback?.Invoke($"🚀 Launching Laragon GUI ({exePath})...");
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = exePath,
                            WorkingDirectory = laragonDir,
                            UseShellExecute = true
                        });
                        logCallback?.Invoke("✅ Laragon main process launched.");
                        await Task.Delay(2000, cancelToken);
                    }
                    catch (Exception ex)
                    {
                        logCallback?.Invoke($"⚠️ Failed launching Laragon GUI: {ex.Message}");
                    }
                }

                // Trigger reload/start argument via laragon.exe
                try
                {
                    logCallback?.Invoke("⚡ Triggering Laragon service reload...");
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = exePath,
                        Arguments = "reload",
                        WorkingDirectory = laragonDir,
                        UseShellExecute = true,
                        CreateNoWindow = true
                    });
                }
                catch { }
            }

            // Step 2: Ensure Apache service (Port 80)
            if (startApache)
            {
                bool apacheActive = await IsPortOpenAsync("127.0.0.1", 80, 500);
                if (apacheActive)
                {
                    logCallback?.Invoke("✅ Apache is active and listening on port 80.");
                }
                else
                {
                    logCallback?.Invoke("⌛ Apache port 80 not responding. Starting httpd.exe directly...");
                    bool launched = TryStartServiceBinary(laragonDir, "apache", "httpd.exe", "", logCallback);
                    if (launched)
                    {
                        await WaitForPortAsync("127.0.0.1", 80, TimeSpan.FromSeconds(8), cancelToken);
                    }
                }
            }

            // Step 3: Ensure MySQL service (Port 3306)
            if (startMySQL)
            {
                bool mysqlActive = await IsPortOpenAsync("127.0.0.1", 3306, 500);
                if (mysqlActive)
                {
                    logCallback?.Invoke("✅ MySQL is active and listening on port 3306.");
                }
                else
                {
                    logCallback?.Invoke("⌛ MySQL port 3306 not responding. Starting mysqld.exe directly...");
                    bool launched = TryStartServiceBinary(laragonDir, "mysql", "mysqld.exe", "--console", logCallback);
                    if (launched)
                    {
                        await WaitForPortAsync("127.0.0.1", 3306, TimeSpan.FromSeconds(8), cancelToken);
                    }
                }
            }

            return true;
        }

        private bool TryStartServiceBinary(string laragonDir, string category, string exeName, string args, Action<string>? logCallback)
        {
            try
            {
                string binDir = Path.Combine(laragonDir, "bin", category);
                if (!Directory.Exists(binDir)) return false;

                string[] matchFiles = Directory.GetFiles(binDir, exeName, SearchOption.AllDirectories);
                if (matchFiles.Length > 0)
                {
                    string targetExe = matchFiles.First();
                    string workingDir = Path.GetDirectoryName(targetExe) ?? laragonDir;

                    logCallback?.Invoke($"⚡ Launching binary: {targetExe}");
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = targetExe,
                        Arguments = args,
                        WorkingDirectory = workingDir,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                    return true;
                }
            }
            catch (Exception ex)
            {
                logCallback?.Invoke($"⚠️ Could not launch {exeName}: {ex.Message}");
            }
            return false;
        }

        private async Task<bool> IsPortOpenAsync(string host, int port, int timeoutMs)
        {
            try
            {
                using var client = new TcpClient();
                var connectTask = client.ConnectAsync(host, port);
                var delayTask = Task.Delay(timeoutMs);
                var completedTask = await Task.WhenAny(connectTask, delayTask);
                return completedTask == connectTask && client.Connected;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> WaitForPortAsync(string host, int port, TimeSpan timeout, CancellationToken cancelToken)
        {
            var start = DateTime.UtcNow;
            while (DateTime.UtcNow - start < timeout)
            {
                if (cancelToken.IsCancellationRequested) return false;
                if (await IsPortOpenAsync(host, port, 500)) return true;
                await Task.Delay(500, cancelToken);
            }
            return false;
        }
    }
}
