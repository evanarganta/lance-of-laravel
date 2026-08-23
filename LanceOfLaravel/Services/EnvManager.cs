using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace LanceOfLaravel.Services
{
    public class EnvManager
    {
        public async Task<bool> EnsureEnvAsync(
            string projectPath,
            bool runMigrations,
            Action<string>? logCallback,
            CancellationToken cancelToken = default)
        {
            string envFile = Path.Combine(projectPath, ".env");
            string envExampleFile = Path.Combine(projectPath, ".env.example");

            if (!File.Exists(envFile))
            {
                logCallback?.Invoke("⚠️ .env file missing!");
                if (File.Exists(envExampleFile))
                {
                    logCallback?.Invoke("📄 Copying '.env.example' to '.env'...");
                    File.Copy(envExampleFile, envFile, overwrite: true);
                    logCallback?.Invoke("✅ .env file created successfully.");

                    logCallback?.Invoke("🔑 Generating application encryption key ('php artisan key:generate')...");
                    bool keyGenSuccess = await RunArtisanCommandAsync(projectPath, "key:generate", logCallback, cancelToken);
                    if (keyGenSuccess)
                    {
                        logCallback?.Invoke("✅ Application key generated!");
                    }
                    else
                    {
                        logCallback?.Invoke("⚠️ Key generation failed. Make sure PHP is available in system PATH.");
                    }
                }
                else
                {
                    logCallback?.Invoke("❌ Neither .env nor .env.example exists in the project root.");
                    return false;
                }
            }
            else
            {
                logCallback?.Invoke("✅ .env file found.");
            }

            // Inspect database configuration in .env
            var dbConfig = InspectDatabaseSettings(envFile, logCallback);

            // Optional migrations
            if (runMigrations)
            {
                if (dbConfig.TryGetValue("DB_CONNECTION", out string? conn) &&
                    conn.Equals("mysql", StringComparison.OrdinalIgnoreCase) &&
                    dbConfig.TryGetValue("DB_DATABASE", out string? dbName) &&
                    !string.IsNullOrWhiteSpace(dbName))
                {
                    logCallback?.Invoke($"🗄️ Checking if database '{dbName}' exists in MySQL...");
                    await AutoCreateMysqlDatabaseAsync(dbConfig, projectPath, logCallback, cancelToken);
                }

                logCallback?.Invoke("🗄️ Running database migrations ('php artisan migrate --force --no-interaction')...");
                bool migrateSuccess = await RunArtisanCommandAsync(projectPath, "migrate --force --no-interaction", logCallback, cancelToken);
                if (migrateSuccess)
                {
                    logCallback?.Invoke("✅ Migrations completed successfully.");
                }
                else
                {
                    logCallback?.Invoke("⚠️ Migration failed. Check if MySQL server is running and database connection parameters match.");
                }
            }

            return true;
        }

        private async Task AutoCreateMysqlDatabaseAsync(
            Dictionary<string, string> dbConfig,
            string workingDir,
            Action<string>? logCallback,
            CancellationToken cancelToken)
        {
            try
            {
                dbConfig.TryGetValue("DB_HOST", out string? host);
                dbConfig.TryGetValue("DB_PORT", out string? port);
                dbConfig.TryGetValue("DB_DATABASE", out string? dbName);
                dbConfig.TryGetValue("DB_USERNAME", out string? user);
                dbConfig.TryGetValue("DB_PASSWORD", out string? pass);

                host = string.IsNullOrWhiteSpace(host) ? "127.0.0.1" : host;
                port = string.IsNullOrWhiteSpace(port) ? "3306" : port;
                user = user ?? "root";
                pass = pass ?? "";

                if (string.IsNullOrWhiteSpace(dbName)) return;

                string phpScript = $"try {{ $pdo = new PDO('mysql:host={host};port={port}', '{user}', '{pass}'); $pdo->exec('CREATE DATABASE IF NOT EXISTS `{dbName}`'); echo 'DB_CREATED'; }} catch (Exception $e) {{ echo 'DB_ERR: ' . $e->getMessage(); }}";

                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c php -r \"{phpScript}\"",
                    WorkingDirectory = workingDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = psi };
                process.Start();
                string output = await process.StandardOutput.ReadToEndAsync(cancelToken);
                await process.WaitForExitAsync(cancelToken);

                if (output.Contains("DB_CREATED"))
                {
                    logCallback?.Invoke($"✅ Database '{dbName}' is ready in MySQL.");
                }
                else if (output.Contains("DB_ERR"))
                {
                    logCallback?.Invoke($"⚠️ Database auto-creation notice: {output.Trim()}");
                }
            }
            catch (Exception ex)
            {
                logCallback?.Invoke($"⚠️ Auto create DB exception: {ex.Message}");
            }
        }

        private Dictionary<string, string> InspectDatabaseSettings(string envPath, Action<string>? logCallback)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                string[] lines = File.ReadAllLines(envPath);
                foreach (var line in lines)
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#')) continue;
                    int idx = trimmed.IndexOf('=');
                    if (idx > 0)
                    {
                        string key = trimmed.Substring(0, idx).Trim();
                        string val = trimmed.Substring(idx + 1).Trim().Trim('"', '\'');
                        dict[key] = val;
                    }
                }

                dict.TryGetValue("DB_CONNECTION", out string? dbConn);
                dict.TryGetValue("DB_DATABASE", out string? dbName);
                dict.TryGetValue("DB_HOST", out string? dbHost);
                dict.TryGetValue("DB_USERNAME", out string? dbUser);

                dbConn = dbConn ?? "sqlite";
                dbName = dbName ?? "laravel";

                logCallback?.Invoke($"ℹ️ Database Config: Connection={dbConn}, DB={dbName}, Host={dbHost ?? "127.0.0.1"}, User={dbUser ?? "root"}");
            }
            catch (Exception ex)
            {
                logCallback?.Invoke($"⚠️ Could not inspect .env file: {ex.Message}");
            }
            return dict;
        }

        private async Task<bool> RunArtisanCommandAsync(
            string workingDir,
            string args,
            Action<string>? logCallback,
            CancellationToken cancelToken)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c php artisan {args}",
                    WorkingDirectory = workingDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = psi };
                process.OutputDataReceived += (s, e) => { if (e.Data != null) logCallback?.Invoke($"  [ARTISAN] {e.Data}"); };
                process.ErrorDataReceived += (s, e) => { if (e.Data != null) logCallback?.Invoke($"  [ARTISAN ERR] {e.Data}"); };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync(cancelToken);
                return process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                logCallback?.Invoke($"❌ Failed running php artisan {args}: {ex.Message}");
                return false;
            }
        }
    }
}
