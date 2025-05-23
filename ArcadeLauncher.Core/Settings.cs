﻿using System.Collections.Generic;

namespace ArcadeLauncher.Core
{
    public class Settings
    {
        public int NumberOfColumns { get; set; } = 5; // Default to 5 columns
        public string DefaultMarqueeImage { get; set; } = "";
        public string DefaultControllerImage { get; set; } = "";
        public Dictionary<string, List<string>> InputMappings { get; set; }
        public double FadeDuration { get; set; } = 5.0; // Default to 0.75 seconds

        public Settings()
        {
            NumberOfColumns = 5; // Default to 5 columns
            DefaultMarqueeImage = "";
            DefaultControllerImage = "";
            InputMappings = new Dictionary<string, List<string>>
            {
                { "Left", new List<string> { "LeftArrow" } },
                { "Right", new List<string> { "RightArrow" } },
                { "Up", new List<string> { "UpArrow" } },
                { "Down", new List<string> { "DownArrow" } },
                { "Select", new List<string> { "Enter" } }, // Renamed from "Launch" to "Select"
                { "Exit", new List<string> { "Escape" } },
                { "Kill", new List<string>() }, // New Kill Switch mapping, empty by default
                { "ToggleOverlay", new List<string>() } // New Toggle Overlay mapping, empty by default
            };
            FadeDuration = 0.75; // Initialize default fade duration
        }
    }
}