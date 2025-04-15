using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ArcadeLauncher.SW1
{
    static class Program
    {
        public static string InstallDir
        {
            get
            {
                return AppDomain.CurrentDomain.BaseDirectory;
            }
        }

        [STAThread]
        static void Main()
        {
            try
            {
                // Set up autostart
                RegistryKey rk = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                rk.SetValue("ArcadeLauncher", Application.ExecutablePath);

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                string logDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ArcadeLauncher");
                string logPath = System.IO.Path.Combine(logDir, "SW1_Error.log");

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