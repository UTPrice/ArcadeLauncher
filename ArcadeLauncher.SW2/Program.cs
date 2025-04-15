using System;
using System.Threading;
using System.Windows.Forms;

namespace ArcadeLauncher.SW2
{
    static class Program
    {
        private const string MutexName = "ArcadeLauncherSW2Mutex"; // Unique identifier for the mutex

        [STAThread]
        static void Main()
        {
            // Check if another instance is already running (Ticket 20)
            bool createdNew;
            using (Mutex mutex = new Mutex(true, MutexName, out createdNew))
            {
                if (!createdNew)
                {
                    // Another instance is already running
                    MessageBox.Show("Arcade Launcher SW2 is already running.", "Single Instance", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return; // Exit the application
                }

                // This is the first instance, proceed with launching the application
                try
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.Run(new MainForm());
                }
                catch (Exception ex)
                {
                    string logDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ArcadeLauncher");
                    string logPath = System.IO.Path.Combine(logDir, "SW2_Error.log");

                    if (!System.IO.Directory.Exists(logDir))
                    {
                        System.IO.Directory.CreateDirectory(logDir);
                    }

                    System.IO.File.WriteAllText(logPath, ex.ToString());
                    MessageBox.Show($"An error occurred: {ex.Message}\nLog saved to: {logPath}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}