using System;
using System.Windows;
using System.Windows.Threading;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ArcadeLauncher.SW3
{
    public partial class MainWindow
    {
        private DispatcherTimer? focusTimer;

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        private void RestoreFocusToMainWindow()
        {
            var mainWindowHandle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            var currentForegroundWindow = GetForegroundWindow();
            if (currentForegroundWindow != mainWindowHandle)
            {
                Dispatcher.Invoke(() =>
                {
                    try
                    {
                        SetForegroundWindow(mainWindowHandle);
                        Activate();
                        Focus();
                        LogToFile($"Restored focus to MainWindow at {DateTime.Now:HH:mm:ss.fff} (handle: {mainWindowHandle}), previous foreground window was: {currentForegroundWindow}");
                    }
                    catch (Exception ex)
                    {
                        LogToFile($"Error restoring focus to MainWindow at {DateTime.Now:HH:mm:ss.fff}: {ex.Message}");
                    }
                });
            }
        }

        private void RestoreFocusToSplashScreen(IntPtr splashHandle)
        {
            try
            {
                SetForegroundWindow(splashHandle);
                splashScreenWindow.Activate();
                splashScreenWindow.Focus();
                LogToFile($"Restored focus to SplashScreenWindow at {DateTime.Now:HH:mm:ss.fff} (handle: {splashHandle})");
            }
            catch (Exception ex)
            {
                LogToFile($"Error restoring focus to SplashScreenWindow at {DateTime.Now:HH:mm:ss.fff}: {ex.Message}");
            }

            // Stop any existing focus timer
            if (focusTimer != null)
            {
                focusTimer.Stop();
                LogToFile($"Stopped existing focus timer at {DateTime.Now:HH:mm:ss.fff}.");
                focusTimer = null;
            }

            focusTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            int focusAttempts = 0;
            focusTimer.Tick += (s, e) =>
            {
                focusAttempts++;
                var currentForeground = GetForegroundWindow();
                if (currentForeground != splashHandle)
                {
                    try
                    {
                        SetForegroundWindow(splashHandle);
                        LogToFile($"Focus attempt {focusAttempts} at {DateTime.Now:HH:mm:ss.fff}: SetForegroundWindow called for SplashScreenWindow, current foreground was {currentForeground}, target handle: {splashHandle}");
                    }
                    catch (Exception ex)
                    {
                        LogToFile($"Error in focus attempt {focusAttempts} at {DateTime.Now:HH:mm:ss.fff}: {ex.Message}");
                    }
                }
                if (focusAttempts >= 20)
                {
                    focusTimer.Stop();
                    focusTimer = null;
                    LogToFile($"Focus loop stopped at {DateTime.Now:HH:mm:ss.fff} after {focusAttempts} attempts.");
                }
            };
            focusTimer.Start();
            LogToFile($"Focus loop started for SplashScreenWindow at {DateTime.Now:HH:mm:ss.fff}.");
        }

        private void StartFocusRestorationLoop()
        {
            var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            var focusTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            int focusAttempts = 0;
            const int maxAttempts = 10;

            focusTimer.Tick += (s, e) =>
            {
                focusAttempts++;
                var currentForegroundWindow = GetForegroundWindow();
                if (currentForegroundWindow != handle)
                {
                    try
                    {
                        SetForegroundWindow(handle);
                        LogToFile($"Focus restoration attempt {focusAttempts} at {DateTime.Now:HH:mm:ss.fff}: SetForegroundWindow called, current foreground window was {currentForegroundWindow}, target handle: {handle}");
                    }
                    catch (Exception ex)
                    {
                        LogToFile($"Error in focus restoration attempt {focusAttempts} at {DateTime.Now:HH:mm:ss.fff}: {ex.Message}");
                    }
                }
                else
                {
                    LogToFile($"Focus restoration successful after {focusAttempts} attempts at {DateTime.Now:HH:mm:ss.fff}: Foreground window is {currentForegroundWindow}, matching target handle: {handle}");
                    focusTimer.Stop();
                }

                if (focusAttempts >= maxAttempts)
                {
                    LogToFile($"Focus restoration loop ended after {maxAttempts} attempts at {DateTime.Now:HH:mm:ss.fff}. Final foreground window: {GetForegroundWindow()}, target handle: {handle}");
                    focusTimer.Stop();
                }
            };
            focusTimer.Start();
            LogToFile($"Started focus restoration loop at {DateTime.Now:HH:mm:ss.fff}.");
        }

        private void StartGameFocusLoop(Process gameProcess)
        {
            var handle = gameProcess.MainWindowHandle;
            if (handle == IntPtr.Zero)
            {
                LogToFile($"Game process MainWindowHandle is zero at {DateTime.Now:HH:mm:ss.fff}. Waiting for valid handle.");
                try
                {
                    gameProcess.WaitForInputIdle(1000);
                    handle = gameProcess.MainWindowHandle;
                }
                catch (Exception ex)
                {
                    LogToFile($"Error waiting for game process input idle at {DateTime.Now:HH:mm:ss.fff}: {ex.Message}");
                }
            }

            var focusTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            int focusAttempts = 0;
            const int maxAttempts = 20;

            focusTimer.Tick += (s, e) =>
            {
                focusAttempts++;
                var currentForeground = GetForegroundWindow();
                if (currentForeground != handle && !gameProcess.HasExited)
                {
                    try
                    {
                        SetForegroundWindow(handle);
                        LogToFile($"Game focus attempt {focusAttempts} at {DateTime.Now:HH:mm:ss.fff}: SetForegroundWindow called for game process, current foreground was {currentForeground}, target handle: {handle}");
                    }
                    catch (Exception ex)
                    {
                        LogToFile($"Error in game focus attempt {focusAttempts} at {DateTime.Now:HH:mm:ss.fff}: {ex.Message}");
                    }
                }
                if (focusAttempts >= maxAttempts || gameProcess.HasExited)
                {
                    focusTimer.Stop();
                    LogToFile($"Game focus loop stopped at {DateTime.Now:HH:mm:ss.fff} after {focusAttempts} attempts. Game exited: {gameProcess.HasExited}");
                }
            };
            focusTimer.Start();
            LogToFile($"Started game focus loop for process handle {handle} at {DateTime.Now:HH:mm:ss.fff}.");
        }
    }
}