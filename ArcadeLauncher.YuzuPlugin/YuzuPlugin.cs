using ArcadeLauncher.Plugins;

namespace ArcadeLauncher.YuzuPlugin
{
    public class YuzuPlugin : IEmulatorPlugin
    {
        public string Name => "Yuzu";

        public string[] SupportedExtensions => new[] { ".nsp", ".xci" };

        public string GetDefaultParameters()
        {
            return "-f"; // Example: -f for fullscreen mode in Yuzu
        }

        public void PreLaunch(string emulatorPath, string romPath)
        {
            // Add any pre-launch logic for Yuzu here, if needed
        }

        public string BuildLaunchCommand(string emulatorPath, string romPath, string customParameters)
        {
            return $"\"{emulatorPath}\" \"{romPath}\" {customParameters}";
        }

        public void PostExit(string emulatorPath, string romPath)
        {
            // Add any post-exit logic for Yuzu here, if needed
        }
    }
}