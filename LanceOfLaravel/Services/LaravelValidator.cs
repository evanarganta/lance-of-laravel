using System;
using System.IO;
using System.Text.Json;
using LanceOfLaravel.Models;

namespace LanceOfLaravel.Services
{
    public class LaravelValidator
    {
        public ProjectModel ValidateProject(string path, string laragonPath = @"C:\laragon\laragon.exe")
        {
            var project = new ProjectModel
            {
                FolderPath = path,
                LastOpened = DateTime.Now
            };

            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                project.IsValidLaravel = false;
                return project;
            }

            string artisanPath = Path.Combine(path, "artisan");
            string composerPath = Path.Combine(path, "composer.json");
            string packagePath = Path.Combine(path, "package.json");

            bool hasArtisan = File.Exists(artisanPath);
            bool hasComposer = File.Exists(composerPath);
            bool hasPackage = File.Exists(packagePath);

            project.IsValidLaravel = hasArtisan && hasComposer && hasPackage;
            project.HasVendor = Directory.Exists(Path.Combine(path, "vendor"));
            project.HasNodeModules = Directory.Exists(Path.Combine(path, "node_modules"));
            project.HasEnv = File.Exists(Path.Combine(path, ".env"));

            // Determine project name from composer.json if available
            string folderName = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            project.Name = folderName;

            if (hasComposer)
            {
                try
                {
                    string composerJsonText = File.ReadAllText(composerPath);
                    using var doc = JsonDocument.Parse(composerJsonText);
                    if (doc.RootElement.TryGetProperty("name", out var nameElement))
                    {
                        string fullName = nameElement.GetString() ?? "";
                        if (!string.IsNullOrEmpty(fullName) && fullName.Contains('/'))
                        {
                            project.Name = fullName.Split('/')[1];
                        }
                    }
                }
                catch
                {
                    // Fallback to folder name
                }
            }

            // Check if project is inside Laragon's www folder
            string laragonWwwFolder = @"C:\laragon\www";
            if (!string.IsNullOrEmpty(laragonPath))
            {
                try
                {
                    string laragonDir = Path.GetDirectoryName(laragonPath) ?? @"C:\laragon";
                    laragonWwwFolder = Path.Combine(laragonDir, "www");
                }
                catch { }
            }

            if (Directory.Exists(laragonWwwFolder))
            {
                string fullPath = Path.GetFullPath(path).TrimEnd('\\', '/');
                string fullWww = Path.GetFullPath(laragonWwwFolder).TrimEnd('\\', '/');
                project.IsLaragonWww = fullPath.StartsWith(fullWww, StringComparison.OrdinalIgnoreCase) && fullPath.Length > fullWww.Length;
            }

            return project;
        }

        public string GetTargetUrl(ProjectModel project, string mode)
        {
            if (mode == "LaragonTest" || (mode == "Auto" && project.IsLaragonWww))
            {
                string folderName = Path.GetFileName(project.FolderPath.TrimEnd('\\', '/'));
                return $"http://{folderName}.test";
            }
            return "http://localhost:8000";
        }
    }
}
