using System.Collections.Generic;

namespace ArcadeLauncher.Core
{
    public class Game
    {
        public string DisplayName { get; set; } = "";
        public string AlphabetizeName { get; set; } = "";
        public string Type { get; set; } = "PC";
        public string EmulatorPlugin { get; set; } = null;
        public string EmulatorPath { get; set; } = "";
        public string RomPath { get; set; } = "";
        public string CustomParameters { get; set; } = "";
        public string ExecutablePath { get; set; } = "";
        public string ArtBoxPath { get; set; } = null;
        public string MarqueePath { get; set; } = null;
        public string ControllerLayoutPath { get; set; } = null;
        public List<string> PreLaunchCommands { get; set; } = new List<string>();
        public List<string> PostExitCommands { get; set; } = new List<string>();
        public string LEDBlinkyCommand { get; set; } = "";
        public string Notes { get; set; } = "";
        public bool IsInProgress { get; set; } = false;
        public bool HideMouseCursor { get; set; } = false;
        // New fields for splash screen
        public Dictionary<string, string> SplashScreenPath { get; set; } = new Dictionary<string, string>(); // e.g., {"4k": path, "1440p": path, "1080p": path}
        public int SplashDuration { get; set; } = 3; // In seconds, default 3

        public override string ToString()
        {
            return $"{DisplayName ?? "Unnamed Game"} (IsInProgress: {IsInProgress}, HideMouseCursor: {HideMouseCursor})";
        }
    }

    public class GameData
    {
        public List<Game> Games { get; set; } = new List<Game>();
    }
}