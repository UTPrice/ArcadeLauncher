using System;
using System.Diagnostics;
using System.IO;

namespace ArcadeLauncher.SW2
{
    // Static utility class for logging
    public static class Logger
    {
        public static void LogToFile(string message)
        {
            try
            {
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string logDir = Path.Combine(appDataPath, "ArcadeLauncher");
                Directory.CreateDirectory(logDir);
                string logFile = Path.Combine(logDir, "SW2_Log.txt");
                File.AppendAllText(logFile, $"{DateTime.Now}: {message}\n");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to log: {ex.Message}");
            }
        }
    }
}