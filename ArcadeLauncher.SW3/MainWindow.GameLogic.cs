using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using ArcadeLauncher.Core;
using ArcadeLauncher.Plugins;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ArcadeLauncher.SW3
{
    public partial class MainWindow
    {
        private Process? activeProcess; // Track the active game process
        private DateTime? lastKillPressTime; // Track the time of the last Kill key press
        private bool isGameActive; // Flag to track game state
        private const int DoublePressThreshold = 500; // 500ms threshold for double-press
        private IntPtr hookId = IntPtr.Zero; // Handle for the keyboard hook

        // P/Invoke declarations for global keyboard hook
        private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private HookProc hookProc;

        private void InitializeGameLogic()
        {
            // Set up the global keyboard hook
            hookProc = HookCallback;
            using (var curProcess = System.Diagnostics.Process.GetCurrentProcess())
            using (var curModule = curProcess.MainModule)
            {
                hookId = SetWindowsHookEx(WH_KEYBOARD_LL, hookProc, GetModuleHandle(curModule.ModuleName), 0);
                if (hookId == IntPtr.Zero)
                {
                    LogToFile($"Failed to set keyboard hook. Error: {Marshal.GetLastWin32Error()}");
                }
                else
                {
                    LogToFile("Keyboard hook successfully set.");
                }
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                string keyString = KeyInterop.KeyFromVirtualKey(vkCode).ToString();
                LogToFile($"Global hook detected key down: {keyString}");

                if (settings != null && settings.InputMappings.ContainsKey("Kill") && settings.InputMappings["Kill"].Any())
                {
                    if (settings.InputMappings["Kill"].Contains(keyString, StringComparer.OrdinalIgnoreCase))
                    {
                        DateTime currentTime = DateTime.Now;
                        if (lastKillPressTime.HasValue && (currentTime - lastKillPressTime.Value).TotalMilliseconds <= DoublePressThreshold)
                        {
                            // Double-press detected
                            Dispatcher.Invoke(() =>
                            {
                                LogToFile($"Global hook: Kill Switch double-press detected for key {keyString}. Active process state: {activeProcess?.HasExited ?? true}");
                                if (isGameActive && activeProcess != null && !activeProcess.HasExited)
                                {
                                    try
                                    {
                                        LogToFile($"Attempting to terminate process: {activeProcess.StartInfo.FileName}");
                                        activeProcess.Kill();
                                        LogToFile($"Process terminated: {activeProcess.StartInfo.FileName}");
                                        PerformPostExitCleanup(); // Manually trigger cleanup
                                        isGameActive = false;
                                    }
                                    catch (Exception ex)
                                    {
                                        LogToFile($"Failed to terminate process: {ex.Message}");
                                    }
                                }
                                else
                                {
                                    LogToFile($"Global hook: No active process to terminate or process already exited.");
                                }
                                lastKillPressTime = null; // Reset after handling
                            });
                        }
                        else
                        {
                            lastKillPressTime = currentTime;
                            LogToFile($"Global hook: Kill Switch single press detected for key {keyString}. Waiting for second press within {DoublePressThreshold}ms.");
                        }
                    }
                }
            }
            return CallNextHookEx(hookId, nCode, wParam, lParam);
        }

        private void PerformPostExitCleanup()
        {
            if (activeProcess != null)
            {
                Dispatcher.Invoke(() =>
                {
                    var game = games?.FirstOrDefault(g => g.ExecutablePath == activeProcess.StartInfo.FileName || (g.Type == "Emulated" && plugins?.FirstOrDefault(p => p.Name == g.EmulatorPlugin)?.BuildLaunchCommand(g.EmulatorPath, g.RomPath, g.CustomParameters) == activeProcess.StartInfo.Arguments));
                    if (game != null)
                    {
                        // Revert Monitor #2 and #3
                        if (marqueeWindow != null && marqueeWindow.Content is System.Windows.Controls.Image marqueeImage2)
                        {
                            string marqueeDefaultPath = System.IO.Path.Combine(Program.InstallDir, "default_marquee.png");
                            SetImageSourceSafely(marqueeImage2, marqueeDefaultPath);
                        }
                        if (controllerWindow != null && controllerWindow.Content is System.Windows.Controls.Image controllerImage2)
                        {
                            string controllerDefaultPath = System.IO.Path.Combine(Program.InstallDir, "default_controller.png");
                            SetImageSourceSafely(controllerImage2, controllerDefaultPath);
                        }

                        // Run Post-Exit Commands
                        foreach (var cmd in game.PostExitCommands ?? new List<string>())
                        {
                            RunCommand(cmd, "Post-Exit Command");
                        }

                        // Tuck the cursor back to the bottom-right corner after game exit
                        var screenWidthLogicalPostExit = SystemParameters.PrimaryScreenWidth;
                        var screenHeightLogicalPostExit = SystemParameters.PrimaryScreenHeight;
                        var screenWidthPhysicalPostExit = (int)(screenWidthLogicalPostExit * dpiScaleFactor);
                        var screenHeightPhysicalPostExit = (int)(screenHeightLogicalPostExit * dpiScaleFactor);
                        System.Windows.Forms.Cursor.Position = new System.Drawing.Point(screenWidthPhysicalPostExit - 1, screenHeightPhysicalPostExit - 1);
                        LogToFile($"Restored mouse cursor after Kill Switch exit: Moved to bottom-right pixel (physical): ({screenWidthPhysicalPostExit - 1}, {screenHeightPhysicalPostExit - 1})");
                    }
                    else
                    {
                        LogToFile("Could not determine game for post-exit cleanup.");
                    }
                    activeProcess = null;
                    isGameActive = false;
                });
            }
        }

        private void MoveSelection(int delta)
        {
            if (settings == null || games == null || gameItemsControl == null || scrollViewer == null) return;

            int newIndex = SelectedIndex + delta;
            int columns = settings.NumberOfColumns > 0 ? settings.NumberOfColumns : 7;
            int rows = (int)Math.Ceiling((double)games.Count / columns);

            // Disable right-left wrapping
            if (delta == 1) // Moving right
            {
                int currentCol = SelectedIndex % columns;
                if (currentCol == columns - 1) // Already at the rightmost column
                {
                    return; // Don't wrap to the left
                }
            }
            else if (delta == -1) // Moving left
            {
                int currentCol = SelectedIndex % columns;
                if (currentCol == 0) // Already at the leftmost column
                {
                    return; // Don't wrap to the right
                }
            }

            // Handle up/down movement with row boundary checks
            if (delta > 0)
            {
                int currentRow = SelectedIndex / columns;
                int currentCol = SelectedIndex % columns;
                int targetRow = (SelectedIndex + delta) / columns;

                if (currentRow == rows - 2 && targetRow == rows - 1)
                {
                    int lastRowArtBoxes = games.Count % columns == 0 ? columns : games.Count % columns;
                    if (currentCol >= lastRowArtBoxes)
                    {
                        newIndex = games.Count - 1;
                    }
                }
            }

            if (newIndex >= 0 && newIndex < games.Count)
            {
                SelectedIndex = newIndex;
                var selectedItem = gameItemsControl.ItemContainerGenerator.ContainerFromIndex(SelectedIndex) as FrameworkElement;
                if (selectedItem != null)
                {
                    selectedItem.Focus(); // Set focus to the selected item
                    ScrollToSelectedItem(); // Scroll to the selected item directly
                    LogToFile($"Moved to PictureBox {SelectedIndex}: Row={SelectedIndex / columns}, Col={SelectedIndex % columns}");
                }
            }
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (settings == null) return;

            if (e.Key == Key.Escape)
            {
                System.Windows.Application.Current.Shutdown();
            }

            string keyString;
            switch (e.Key)
            {
                case Key.Left:
                    keyString = "LeftArrow";
                    break;
                case Key.Right:
                    keyString = "RightArrow";
                    break;
                case Key.Up:
                    keyString = "UpArrow";
                    break;
                case Key.Down:
                    keyString = "DownArrow";
                    break;
                default:
                    keyString = e.Key.ToString();
                    break;
            }

            if (settings.InputMappings["Left"].Contains(keyString, StringComparer.OrdinalIgnoreCase))
            {
                MoveSelection(-1);
            }
            else if (settings.InputMappings["Right"].Contains(keyString, StringComparer.OrdinalIgnoreCase))
            {
                MoveSelection(1);
            }
            else if (settings.InputMappings["Up"].Contains(keyString, StringComparer.OrdinalIgnoreCase))
            {
                MoveSelection(-settings.NumberOfColumns);
            }
            else if (settings.InputMappings["Down"].Contains(keyString, StringComparer.OrdinalIgnoreCase))
            {
                MoveSelection(settings.NumberOfColumns);
            }
            else if (settings.InputMappings["Select"].Contains(keyString, StringComparer.OrdinalIgnoreCase))
            {
                if (games != null && SelectedIndex >= 0 && SelectedIndex < games.Count)
                {
                    LaunchGame(games[SelectedIndex]);
                }
            }
            else if (settings.InputMappings.ContainsKey("Exit") && settings.InputMappings["Exit"].Contains(keyString, StringComparer.OrdinalIgnoreCase))
            {
                System.Windows.Application.Current.Shutdown();
            }
        }

        private void Window_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // No timers or state flags to manage in the minimal implementation
        }

        private void RunCommand(string command, string description)
        {
            try
            {
                Process.Start("cmd.exe", $"/c {command}");
                LogToFile($"Successfully executed {description}: {command}");
            }
            catch (Exception ex)
            {
                LogToFile($"Failed to execute {description}: {command}, Error: {ex.Message}");
            }
        }

        private void LaunchGame(Game game)
        {
            try
            {
                // Log the game launch details
                LogToFile($"Launching game: {game.DisplayName}, HideMouseCursor: {game.HideMouseCursor}");

                // Adjust cursor position based on HideMouseCursor setting
                var screenWidthLogical = SystemParameters.PrimaryScreenWidth;
                var screenHeightLogical = SystemParameters.PrimaryScreenHeight;
                var screenWidthPhysical = (int)(screenWidthLogical * dpiScaleFactor);
                var screenHeightPhysical = (int)(screenHeightLogical * dpiScaleFactor);

                if (game.HideMouseCursor)
                {
                    // Leave the cursor in the bottom-right corner (already positioned there)
                    LogToFile($"HideMouseCursor is true, leaving cursor at bottom-right pixel (physical): ({screenWidthPhysical - 1}, {screenHeightPhysical - 1})");
                }
                else
                {
                    // Move the cursor to the center of the screen
                    int centerX = screenWidthPhysical / 2;
                    int centerY = screenHeightPhysical / 2;
                    System.Windows.Forms.Cursor.Position = new System.Drawing.Point(centerX, centerY);
                    LogToFile($"HideMouseCursor is false, moved cursor to center of screen (physical): ({centerX}, {centerY})");
                }

                // Update Monitor #2 and #3
                if (marqueeWindow != null && marqueeWindow.Content is System.Windows.Controls.Image marqueeImage)
                {
                    string marqueePath = game.MarqueePath ?? System.IO.Path.Combine(Program.InstallDir, "default_marquee.png");
                    SetImageSourceSafely(marqueeImage, marqueePath);
                }
                if (controllerWindow != null && controllerWindow.Content is System.Windows.Controls.Image controllerImage)
                {
                    string controllerPath = game.ControllerLayoutPath ?? System.IO.Path.Combine(Program.InstallDir, "default_controller.png");
                    SetImageSourceSafely(controllerImage, controllerPath);
                }

                // Run Pre-Launch Commands
                foreach (var cmd in game.PreLaunchCommands ?? new List<string>())
                {
                    RunCommand(cmd, "Pre-Launch Command");
                }

                // Run LEDBlinky Animation
                if (!string.IsNullOrEmpty(game.LEDBlinkyCommand))
                {
                    RunCommand(game.LEDBlinkyCommand, "LEDBlinky Command");
                }

                // Launch Game
                if (game.Type == "PC")
                {
                    Process gameProcess = new Process();
                    gameProcess.StartInfo.FileName = game.ExecutablePath;
                    gameProcess.StartInfo.UseShellExecute = true; // Required for proper process handling
                    gameProcess.EnableRaisingEvents = true;
                    isGameActive = true; // Set game active flag
                    gameProcess.Exited += (s, e) =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            // Revert Monitor #2 and #3
                            if (marqueeWindow != null && marqueeWindow.Content is System.Windows.Controls.Image marqueeImage2)
                            {
                                string marqueeDefaultPath = System.IO.Path.Combine(Program.InstallDir, "default_marquee.png");
                                SetImageSourceSafely(marqueeImage2, marqueeDefaultPath);
                            }
                            if (controllerWindow != null && controllerWindow.Content is System.Windows.Controls.Image controllerImage2)
                            {
                                string controllerDefaultPath = System.IO.Path.Combine(Program.InstallDir, "default_controller.png");
                                SetImageSourceSafely(controllerImage2, controllerDefaultPath);
                            }

                            // Run Post-Exit Commands
                            foreach (var cmd in game.PostExitCommands ?? new List<string>())
                            {
                                RunCommand(cmd, "Post-Exit Command");
                            }

                            // Tuck the cursor back to the bottom-right corner after game exit
                            var screenWidthLogicalPostExit = SystemParameters.PrimaryScreenWidth;
                            var screenHeightLogicalPostExit = SystemParameters.PrimaryScreenHeight;
                            var screenWidthPhysicalPostExit = (int)(screenWidthLogicalPostExit * dpiScaleFactor);
                            var screenHeightPhysicalPostExit = (int)(screenHeightLogicalPostExit * dpiScaleFactor);
                            System.Windows.Forms.Cursor.Position = new System.Drawing.Point(screenWidthPhysicalPostExit - 1, screenHeightPhysicalPostExit - 1);
                            LogToFile($"Restored mouse cursor after PC game exit: Moved to bottom-right pixel (physical): ({screenWidthPhysicalPostExit - 1}, {screenHeightPhysicalPostExit - 1})");

                            // Clear the active process and state
                            activeProcess = null;
                            isGameActive = false;
                        });
                    };
                    gameProcess.Start();
                    activeProcess = gameProcess; // Set the active process
                    LogToFile($"Launched PC game: {game.ExecutablePath}");
                }
                else if (game.Type == "Emulated")
                {
                    var plugin = plugins.FirstOrDefault(p => p.Name == game.EmulatorPlugin);
                    if (plugin != null)
                    {
                        plugin.PreLaunch(game.EmulatorPath, game.RomPath);
                        var cmd = plugin.BuildLaunchCommand(game.EmulatorPath, game.RomPath, game.CustomParameters);
                        Process emulatorProcess = new Process();
                        emulatorProcess.StartInfo.FileName = "cmd.exe";
                        emulatorProcess.StartInfo.Arguments = $"/c {cmd}";
                        emulatorProcess.StartInfo.UseShellExecute = true;
                        emulatorProcess.EnableRaisingEvents = true;
                        isGameActive = true; // Set game active flag
                        emulatorProcess.Exited += (s, e) =>
                        {
                            Dispatcher.Invoke(() =>
                            {
                                plugin.PostExit(game.EmulatorPath, game.RomPath);
                                // Revert Monitor #2 and #3
                                if (marqueeWindow != null && marqueeWindow.Content is System.Windows.Controls.Image marqueeImage2)
                                {
                                    string marqueeDefaultPath = System.IO.Path.Combine(Program.InstallDir, "default_marquee.png");
                                    SetImageSourceSafely(marqueeImage2, marqueeDefaultPath);
                                }
                                if (controllerWindow != null && controllerWindow.Content is System.Windows.Controls.Image controllerImage2)
                                {
                                    string controllerDefaultPath = System.IO.Path.Combine(Program.InstallDir, "default_controller.png");
                                    SetImageSourceSafely(controllerImage2, controllerDefaultPath);
                                }

                                // Run Post-Exit Commands
                                foreach (var cmd in game.PostExitCommands ?? new List<string>())
                                {
                                    RunCommand(cmd, "Post-Exit Command");
                                }

                                // Tuck the cursor back to the bottom-right corner after game exit
                                var screenWidthLogicalPostExit = SystemParameters.PrimaryScreenWidth;
                                var screenHeightLogicalPostExit = SystemParameters.PrimaryScreenHeight;
                                var screenWidthPhysicalPostExit = (int)(screenWidthLogicalPostExit * dpiScaleFactor);
                                var screenHeightPhysicalPostExit = (int)(screenHeightLogicalPostExit * dpiScaleFactor);
                                System.Windows.Forms.Cursor.Position = new System.Drawing.Point(screenWidthPhysicalPostExit - 1, screenHeightPhysicalPostExit - 1);
                                LogToFile($"Restored mouse cursor after emulated game exit: Moved to bottom-right pixel (physical): ({screenWidthPhysicalPostExit - 1}, {screenHeightPhysicalPostExit - 1})");

                                // Clear the active process and state
                                activeProcess = null;
                                isGameActive = false;
                            });
                        };
                        emulatorProcess.Start();
                        activeProcess = emulatorProcess; // Set the active process
                        LogToFile($"Launched emulated game: {cmd}");
                    }
                    else
                    {
                        LogToFile($"Emulator plugin not found for game type: {game.EmulatorPlugin}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogToFile($"Failed to launch game: {ex.Message}");
                var messageBox = new Window
                {
                    Title = "Error",
                    Width = 400,
                    Height = 200,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };
                var label = new TextBlock
                {
                    Text = $"Application failed to Launch\n\nDetails: {ex.Message}",
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                messageBox.Content = label;
                messageBox.KeyDown += (s, e) =>
                {
                    if (settings != null && settings.InputMappings["Select"].Contains(e.Key.ToString(), StringComparer.OrdinalIgnoreCase))
                    {
                        messageBox.Close();
                    }
                };
                messageBox.ShowDialog();

                // Tuck the cursor back to the bottom-right corner after launch failure
                var screenWidthLogicalPostFailure = SystemParameters.PrimaryScreenWidth;
                var screenHeightLogicalPostFailure = SystemParameters.PrimaryScreenHeight;
                var screenWidthPhysicalPostFailure = (int)(screenWidthLogicalPostFailure * dpiScaleFactor);
                var screenHeightPhysicalPostFailure = (int)(screenHeightLogicalPostFailure * dpiScaleFactor);
                System.Windows.Forms.Cursor.Position = new System.Drawing.Point(screenWidthPhysicalPostFailure - 1, screenHeightPhysicalPostFailure - 1);
                LogToFile($"Restored mouse cursor after launch failure: Moved to bottom-right pixel (physical): ({screenWidthPhysicalPostFailure - 1}, {screenHeightPhysicalPostFailure - 1})");
            }
        }
    }
}