using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using LanceOfLaravel.Models;
using LanceOfLaravel.Services;

namespace LanceOfLaravel
{
    public partial class MainWindow : Window
    {
        private readonly ProjectHistoryService _historyService = new ProjectHistoryService();
        private readonly LaravelValidator _validator = new LaravelValidator();
        private readonly DependencyManager _dependencyManager = new DependencyManager();
        private readonly EnvManager _envManager = new EnvManager();
        private readonly LaragonManager _laragonManager = new LaragonManager();
        private readonly DevServerRunner _serverRunner = new DevServerRunner();

        private AppConfig _config = new AppConfig();
        private ProjectModel? _currentProject;
        private CancellationTokenSource? _cts;

        public MainWindow()
        {
            InitializeComponent();
            LoadAppConfig();
        }

        private void LoadAppConfig()
        {
            _config = _historyService.LoadConfig();

            ChkStartLaragon.IsChecked = _config.AutoStartLaragon;
            ChkStartApache.IsChecked = _config.AutoStartApache;
            ChkStartMySQL.IsChecked = _config.AutoStartMySQL;
            ChkAutoMigrate.IsChecked = _config.AutoRunMigrations;
            ChkForceSetup.IsChecked = _config.ForceSetup;

            RefreshRecentProjectsUI();

            if (!string.IsNullOrEmpty(_config.LastSelectedProjectPath) && Directory.Exists(_config.LastSelectedProjectPath))
            {
                SelectProjectFolder(_config.LastSelectedProjectPath);
            }
        }

        private void SaveAppConfig()
        {
            _config.AutoStartLaragon = ChkStartLaragon.IsChecked ?? true;
            _config.AutoStartApache = ChkStartApache.IsChecked ?? true;
            _config.AutoStartMySQL = ChkStartMySQL.IsChecked ?? true;
            _config.AutoRunMigrations = ChkAutoMigrate.IsChecked ?? false;
            _config.ForceSetup = ChkForceSetup.IsChecked ?? false;
            if (_currentProject != null)
            {
                _config.LastSelectedProjectPath = _currentProject.FolderPath;
            }

            _historyService.SaveConfig(_config);
        }

        private void RefreshRecentProjectsUI()
        {
            LstRecentProjects.ItemsSource = null;
            LstRecentProjects.ItemsSource = _config.RecentProjects.OrderByDescending(p => p.LastOpened).ToList();
        }

        private void SelectProjectFolder(string path)
        {
            _currentProject = _validator.ValidateProject(path, _config.LaragonPath);
            TxtSelectedPath.Text = _currentProject.FolderPath;

            if (_currentProject.IsValidLaravel)
            {
                BdrValidationBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
                TxtValidationStatus.Text = $"✅ Valid Laravel Project ({_currentProject.DisplayName})";
                Log($"[OK] Selected valid Laravel project: {_currentProject.FolderPath}");
            }
            else
            {
                BdrValidationBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
                TxtValidationStatus.Text = "❌ This doesn't appear to be a Laravel project.";
                Log($"[ERROR] Selected directory is not a valid Laravel project: {path}");
            }

            // Save to recent projects list
            var existing = _config.RecentProjects.FirstOrDefault(p => p.FolderPath.Equals(path, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                _config.RecentProjects.Remove(existing);
            }

            if (_currentProject.IsValidLaravel)
            {
                _config.RecentProjects.Insert(0, _currentProject);
                if (_config.RecentProjects.Count > 10)
                {
                    _config.RecentProjects = _config.RecentProjects.Take(10).ToList();
                }
            }

            SaveAppConfig();
            RefreshRecentProjectsUI();
        }

        private void BtnBrowseProject_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select Laravel Project Directory",
                Multiselect = false
            };

            if (dialog.ShowDialog() == true)
            {
                SelectProjectFolder(dialog.FolderName);
            }
        }

        private void LstRecentProjects_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstRecentProjects.SelectedItem is ProjectModel selectedProject)
            {
                if (Directory.Exists(selectedProject.FolderPath))
                {
                    SelectProjectFolder(selectedProject.FolderPath);
                }
                else
                {
                    MessageBox.Show($"Folder no longer exists: {selectedProject.FolderPath}", "Folder Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private async void BtnRunProject_Click(object sender, RoutedEventArgs e)
        {
            if (_currentProject == null || string.IsNullOrWhiteSpace(_currentProject.FolderPath))
            {
                MessageBox.Show("Please select a Laravel project folder first.", "No Project Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SaveAppConfig();
            SetUiStateRunning(true);
            _cts = new CancellationTokenSource();

            Log("\n=========================================");
            Log($"🚀 STARTING LARAVEL PROJECT: {_currentProject.DisplayName}");
            Log($"📂 Path: {_currentProject.FolderPath}");
            Log("=========================================\n");

            try
            {
                // STEP 1: Check Laravel validity
                UpdateStatus("Step 1/6: Validating Laravel Project...");
                Log("🔍 Step 1: Checking Laravel project files...");
                var validated = _validator.ValidateProject(_currentProject.FolderPath, _config.LaragonPath);

                if (!validated.IsValidLaravel)
                {
                    Log("❌ This doesn't appear to be a Laravel project.");
                    Log("   Missing one or more required files: artisan, composer.json, package.json.");
                    MessageBox.Show("❌ This doesn't appear to be a Laravel project.\nMissing artisan, composer.json, or package.json.", "Validation Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    UpdateStatus("Error: Not a Laravel Project", "#EF4444");
                    SetUiStateRunning(false);
                    return;
                }
                Log("✅ Laravel project files verified (artisan, composer.json, package.json present).");

                // STEP 2: Restore Dependencies
                UpdateStatus("Step 2/6: Checking & Restoring Dependencies...");
                Log("\n🔍 Step 2: Restoring dependencies if missing...");
                bool forceSetup = ChkForceSetup.IsChecked ?? false;
                bool depsOk = await _dependencyManager.EnsureDependenciesAsync(
                    validated.FolderPath,
                    forceSetup,
                    Log,
                    _cts.Token
                );

                if (!depsOk)
                {
                    Log("❌ Dependency restoration failed.");
                    UpdateStatus("Error: Dependencies Failed", "#EF4444");
                    SetUiStateRunning(false);
                    return;
                }

                // STEP 3: Handle .env & APP_KEY & Migrations
                UpdateStatus("Step 3/6: Initializing Environment (.env)...");
                Log("\n🔍 Step 3: Checking environment configuration (.env)...");
                bool autoMigrate = ChkAutoMigrate.IsChecked ?? false;
                bool envOk = await _envManager.EnsureEnvAsync(
                    validated.FolderPath,
                    autoMigrate,
                    Log,
                    _cts.Token
                );

                if (!envOk)
                {
                    Log("❌ Environment initialization failed.");
                    UpdateStatus("Error: .env Failed", "#EF4444");
                    SetUiStateRunning(false);
                    return;
                }

                // STEP 4: Start Laragon & Services (Apache + MySQL)
                UpdateStatus("Step 4/6: Managing Laragon & Services...");
                Log("\n🔍 Step 4: Checking Laragon & MySQL/Apache services...");
                bool startLaragon = ChkStartLaragon.IsChecked ?? true;
                bool startApache = ChkStartApache.IsChecked ?? true;
                bool startMySQL = ChkStartMySQL.IsChecked ?? true;

                await _laragonManager.EnsureLaragonServicesAsync(
                    _config.LaragonPath,
                    startLaragon,
                    startApache,
                    startMySQL,
                    Log,
                    _cts.Token
                );

                // STEP 5: Determine Target URL & Mode
                UpdateStatus("Step 5/6: Preparing Execution Mode...");
                string modeSetting = "Auto";
                if (CmbExecutionMode.SelectedIndex == 1) modeSetting = "Localhost";
                else if (CmbExecutionMode.SelectedIndex == 2) modeSetting = "LaragonTest";

                string targetUrl = _validator.GetTargetUrl(validated, modeSetting);
                bool isLaragonMode = targetUrl.Contains(".test");

                Log($"\n🌐 Execution Mode Selected: {(isLaragonMode ? "Laragon Virtual Host (.test)" : "Localhost Development Server (:8000)")}");
                Log($"🎯 Target URL: {targetUrl}");

                // STEP 6: Run Server & Auto Open Browser
                UpdateStatus("Step 6/6: Starting Server & Launching Browser...");
                bool serverStarted = await _serverRunner.StartServerAsync(
                    validated.FolderPath,
                    targetUrl,
                    isLaragonMode,
                    Log,
                    _cts.Token
                );

                if (serverStarted)
                {
                    UpdateStatus($"🚀 Running at {targetUrl}", "#10B981");
                    BtnStopServer.IsEnabled = true;
                }
                else
                {
                    UpdateStatus("Warning: Server started with issues", "#F59E0B");
                }
            }
            catch (OperationCanceledException)
            {
                Log("\n⏹ Launch operation cancelled by user.");
                UpdateStatus("Cancelled", "#94A3B8");
            }
            catch (Exception ex)
            {
                Log($"\n❌ Exception during launch: {ex.Message}");
                UpdateStatus("Error: Launch Failed", "#EF4444");
            }
            finally
            {
                BtnRunProject.IsEnabled = true;
                BtnForceSetup.IsEnabled = true;
            }
        }

        private async void BtnForceSetup_Click(object sender, RoutedEventArgs e)
        {
            if (_currentProject == null || !Directory.Exists(_currentProject.FolderPath))
            {
                MessageBox.Show("Please select a valid Laravel project folder first.", "No Project Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SetUiStateRunning(true);
            _cts = new CancellationTokenSource();
            Log("\n⚡ RUNNING FORCE SETUP (Reinstalling Dependencies & Re-generating Key)...");

            try
            {
                await _dependencyManager.EnsureDependenciesAsync(_currentProject.FolderPath, forceSetup: true, Log, _cts.Token);
                await _envManager.EnsureEnvAsync(_currentProject.FolderPath, runMigrations: false, Log, _cts.Token);
                Log("✅ Force setup completed successfully!");
                UpdateStatus("Setup Complete", "#10B981");
            }
            catch (Exception ex)
            {
                Log($"❌ Setup error: {ex.Message}");
                UpdateStatus("Setup Error", "#EF4444");
            }
            finally
            {
                SetUiStateRunning(false);
            }
        }

        private void BtnStopServer_Click(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
            _serverRunner.StopAllServers(Log);
            BtnStopServer.IsEnabled = false;
            UpdateStatus("Stopped", "#94A3B8");
            Log("⏹ All dev servers stopped.");
        }

        private void SetUiStateRunning(bool isRunning)
        {
            BtnRunProject.IsEnabled = !isRunning;
            BtnForceSetup.IsEnabled = !isRunning;
        }

        private void UpdateStatus(string message, string hexColor = "#10B981")
        {
            Dispatcher.Invoke(() =>
            {
                TxtStatus.Text = message;
                TxtStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hexColor));
            });
        }

        private void Log(string message)
        {
            Dispatcher.Invoke(() =>
            {
                TxtLogs.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
                TxtLogs.ScrollToEnd();
            });
        }

        private void BtnClearLog_Click(object sender, RoutedEventArgs e)
        {
            TxtLogs.Clear();
        }

        private void BtnCopyLog_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(TxtLogs.Text))
            {
                Clipboard.SetText(TxtLogs.Text);
                MessageBox.Show("Logs copied to clipboard!", "Copied", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select Laragon Executable (laragon.exe)",
                Filter = "Executables (*.exe)|*.exe|All Files (*.*)|*.*",
                FileName = "laragon.exe"
            };

            if (dialog.ShowDialog() == true)
            {
                _config.LaragonPath = dialog.FileName;
                SaveAppConfig();
                Log($"⚙️ Laragon path updated to: {_config.LaragonPath}");
                MessageBox.Show($"Laragon path saved:\n{_config.LaragonPath}", "Settings Saved", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _cts?.Cancel();
            _serverRunner.StopAllServers();
            base.OnClosed(e);
        }
    }
}