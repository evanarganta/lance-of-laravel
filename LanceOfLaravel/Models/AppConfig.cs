using System.Collections.Generic;

namespace LanceOfLaravel.Models
{
    public class AppConfig
    {
        public string LaragonPath { get; set; } = @"C:\laragon\laragon.exe";
        public bool AutoStartLaragon { get; set; } = true;
        public bool AutoStartApache { get; set; } = true;
        public bool AutoStartMySQL { get; set; } = true;
        public bool AutoRunMigrations { get; set; } = false;
        public bool ForceSetup { get; set; } = false;
        public string LastSelectedProjectPath { get; set; } = string.Empty;
        public List<ProjectModel> RecentProjects { get; set; } = new List<ProjectModel>();
    }
}
