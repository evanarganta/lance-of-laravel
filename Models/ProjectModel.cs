using System;
using System.Text.Json.Serialization;

namespace LaravelLauncher.Models
{
    public class ProjectModel
    {
        public string Name { get; set; } = string.Empty;
        public string FolderPath { get; set; } = string.Empty;
        public DateTime LastOpened { get; set; } = DateTime.Now;
        public string PreferredMode { get; set; } = "Auto"; // "Auto", "Localhost", "LaragonTest"
        public bool IsLaragonWww { get; set; }
        public bool HasVendor { get; set; }
        public bool HasNodeModules { get; set; }
        public bool HasEnv { get; set; }
        public bool IsValidLaravel { get; set; }

        [JsonIgnore]
        public string DisplayName => string.IsNullOrWhiteSpace(Name) ? System.IO.Path.GetFileName(FolderPath) : Name;

        [JsonIgnore]
        public string ModeBadge => IsLaragonWww ? "Laragon (.test)" : "Localhost (:8000)";
    }
}
