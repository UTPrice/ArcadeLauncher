namespace ArcadeLauncher.Plugins
{
    public interface IEmulatorPlugin
    {
        string Name { get; }
        string[] SupportedExtensions { get; }
        string GetDefaultParameters();
        string BuildLaunchCommand(string emulatorPath, string romPath, string customParameters);
        void PreLaunch(string emulatorPath, string romPath);
        void PostExit(string emulatorPath, string romPath);
    }
}