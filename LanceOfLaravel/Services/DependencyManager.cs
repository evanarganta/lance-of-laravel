using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace LanceOfLaravel.Services
{
    public class DependencyManager
    {
        public async Task<bool> EnsureDependenciesAsync(
            string projectPath,
            bool forceSetup,
            Action<string>? logCallback,
            CancellationToken cancelToken = default)
        {
            bool vendorMissing = !Directory.Exists(Path.Combine(projectPath, "vendor"));
            bool nodeModulesMissing = !Directory.Exists(Path.Combine(projectPath, "node_modules"));

            bool composerSuccess = true;
            bool npmSuccess = true;

            if (vendorMissing || forceSetup)
            {
                logCallback?.Invoke(vendorMissing ? "📦 vendor/ folder missing. Running 'composer install'..." : "⚡ Force setup requested: Running 'composer install'...");
                composerSuccess = await RunCommandAsync("composer", "install", projectPath, logCallback, cancelToken);
                if (!composerSuccess)
                {
                    logCallback?.Invoke("❌ Composer install failed. Please check the logs above.");
                    return false;
                }
                logCallback?.Invoke("✅ Composer dependencies restored successfully.");
            }
            else
            {
                logCallback?.Invoke("✅ vendor/ folder exists (Composer dependencies ready).");
            }

            if (nodeModulesMissing || forceSetup)
            {
                logCallback?.Invoke(nodeModulesMissing ? "📦 node_modules/ folder missing. Running 'npm install'..." : "⚡ Force setup requested: Running 'npm install'...");
                npmSuccess = await RunCommandAsync("npm", "install", projectPath, logCallback, cancelToken);
                if (!npmSuccess)
                {
                    logCallback?.Invoke("⚠️ npm install ended with warnings/errors. Proceeding...");
                }
                else
                {
                    logCallback?.Invoke("✅ Node dependencies restored successfully.");
                }
            }
            else
            {
                logCallback?.Invoke("✅ node_modules/ folder exists (Node dependencies ready).");
            }

            return composerSuccess;
        }

        private async Task<bool> RunCommandAsync(
            string fileName,
            string arguments,
            string workingDir,
            Action<string>? logCallback,
            CancellationToken cancelToken)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {fileName} {arguments}",
                    WorkingDirectory = workingDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = psi };
                var tcs = new TaskCompletionSource<bool>();

                process.OutputDataReceived += (s, e) =>
                {
                    if (e.Data != null) logCallback?.Invoke($"  [STDOUT] {e.Data}");
                };

                process.ErrorDataReceived += (s, e) =>
                {
                    if (e.Data != null) logCallback?.Invoke($"  [STDERR] {e.Data}");
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                using (cancelToken.Register(() =>
                {
                    try { if (!process.HasExited) process.Kill(true); } catch { }
                }))
                {
                    await process.WaitForExitAsync(cancelToken);
                }

                return process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                logCallback?.Invoke($"❌ Error executing command '{fileName} {arguments}': {ex.Message}");
                return false;
            }
        }
    }
}
