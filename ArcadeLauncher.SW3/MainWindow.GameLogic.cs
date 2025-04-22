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
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Windows.Media;

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

        // P/Invoke for process tree termination
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

        // P/Invoke for focus restoration
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        private const uint PROCESS_TERMINATE = 0x0001;
        private const uint PROCESS_QUERY_INFORMATION = 0x0400;

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

                // If no game is running, ensure MainWindow has focus before processing the key
                if (!isGameActive)
                {
                    var mainWindowHandle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                    var currentForegroundWindow = GetForegroundWindow();
                    if (currentForegroundWindow != mainWindowHandle)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            SetForegroundWindow(mainWindowHandle);
                            Activate();
                            Focus();
                            LogToFile($"Restored focus to MainWindow on key down (handle: {mainWindowHandle}), previous foreground window was: {currentForegroundWindow}");
                        });
                    }
                }

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
                        // Clean up the process tree to ensure no child processes linger
                        try
                        {
                            if (!activeProcess.HasExited)
                            {
                                KillProcessTree(activeProcess.Id);
                                LogToFile($"Terminated process tree for process ID: {activeProcess.Id}");
                            }
                        }
                        catch (Exception ex)
                        {
                            LogToFile($"Failed to terminate process tree: {ex.Message}");
                        }

                        // Show the splash screen again before reverting to the game selection screen
                        splashScreenWindow = new SplashScreenWindow(this, game, dpiScaleFactor, () =>
                        {
                            // Revert Monitor #2 and #3 with fade animations
                            if (marqueeWindow != null && marqueeWindow.Content is Grid marqueeGrid)
                            {
                                string marqueeDefaultPath = System.IO.Path.Combine(Program.InstallDir, "default_marquee.png");
                                FadeImage(marqueeGrid, new BitmapImage(new Uri(marqueeDefaultPath, UriKind.Absolute)));
                            }
                            if (controllerWindow != null && controllerWindow.Content is Grid controllerGrid)
                            {
                                string controllerDefaultPath = System.IO.Path.Combine(Program.InstallDir, "default_controller.png");
                                FadeImage(controllerGrid, new BitmapImage(new Uri(controllerDefaultPath, UriKind.Absolute)));
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

                            // Clear the active process and state
                            activeProcess = null;
                            isGameActive = false;
                        }, isLaunchPhase: false); // Exit phase

                        // Ensure the splash screen is visible and focused
                        splashScreenWindow.Opacity = 0; // Start with 0 opacity
                        splashScreenWindow.Visibility = Visibility.Visible;
                        splashScreenWindow.Show();
                        splashScreenWindow.Activate();
                        splashScreenWindow.Focus();
                        LogToFile($"SplashScreenWindow shown for exit phase. Visibility: {splashScreenWindow.Visibility}, Opacity: {splashScreenWindow.Opacity}");

                        // Ensure the splash screen is rendered
                        Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);
                        LogToFile("SplashScreenWindow render forced after Show() in exit phase.");

                        // Hide the game selection screen during splash screen display
                        Visibility = Visibility.Hidden;
                        Opacity = 0; // Ensure MainWindow starts at 0 opacity for cross-fade
                    }
                    else
                    {
                        LogToFile("Could not determine game for post-exit cleanup.");
                        // Still need to reset state
                        activeProcess = null;
                        isGameActive = false;
                        Visibility = Visibility.Visible;
                        Topmost = true;
                        Activate();
                        Focus();

                        // Use P/Invoke to ensure the window is the foreground window
                        var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                        SetForegroundWindow(handle);
                        LogToFile($"Game selection screen restored (no game found) and set to foreground window with handle: {handle}");

                        // Start a focus restoration loop to ensure focus is maintained
                        StartFocusRestorationLoop();
                    }
                });
            }
        }

        private void StartFocusRestorationLoop()
        {
            var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            var focusTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200) // Check every 200ms
            };
            int focusAttempts = 0;
            const int maxAttempts = 10; // Try for 2 seconds (10 * 200ms)

            focusTimer.Tick += (s, e) =>
            {
                focusAttempts++;
                var currentForegroundWindow = GetForegroundWindow();
                if (currentForegroundWindow != handle)
                {
                    SetForegroundWindow(handle);
                    LogToFile($"Focus restoration attempt {focusAttempts}: SetForegroundWindow called, current foreground window was {currentForegroundWindow}, target handle: {handle}");
                }
                else
                {
                    LogToFile($"Focus restoration successful after {focusAttempts} attempts: Foreground window is {currentForegroundWindow}, matching target handle: {handle}");
                    focusTimer.Stop();
                }

                if (focusAttempts >= maxAttempts)
                {
                    LogToFile($"Focus restoration loop ended after {maxAttempts} attempts. Final foreground window: {GetForegroundWindow()}, target handle: {handle}");
                    focusTimer.Stop();
                }
            };
            focusTimer.Start();
            LogToFile("Started focus restoration loop.");
        }

        private void KillProcessTree(int processId)
        {
            // Kill the main process
            IntPtr hProcess = OpenProcess(PROCESS_TERMINATE | PROCESS_QUERY_INFORMATION, false, processId);
            if (hProcess != IntPtr.Zero)
            {
                try
                {
                    TerminateProcess(hProcess, 1);
                }
                finally
                {
                    CloseHandle(hProcess);
                }
            }

            // Find and kill child processes
            var processes = Process.GetProcesses();
            foreach (var process in processes)
            {
                try
                {
                    if (GetParentProcessId(process.Id) == processId)
                    {
                        KillProcessTree(process.Id); // Recursively kill child processes
                    }
                }
                catch (Exception ex)
                {
                    LogToFile($"Error while killing child process {process.Id}: {ex.Message}");
                }
                finally
                {
                    process.Dispose();
                }
            }
        }

        private int GetParentProcessId(int processId)
        {
            try
            {
                using (var process = Process.GetProcessById(processId))
                {
                    var parentEntry = process.StartInfo; // Note: This is a simplification; in reality, you'd need to use P/Invoke to NtQueryInformationProcess to get the parent process ID reliably
                    return process.Id; // Placeholder; actual implementation requires P/Invoke
                }
            }
            catch
            {
                return -1;
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

        private void FadeImage(Grid grid, ImageSource toSource)
        {
            // Ensure the grid has two Image controls (current and next)
            var currentImage = grid.Children.OfType<System.Windows.Controls.Image>().FirstOrDefault(i => i.Name == "CurrentImage");
            var nextImage = grid.Children.OfType<System.Windows.Controls.Image>().FirstOrDefault(i => i.Name == "NextImage");

            if (currentImage == null || nextImage == null)
            {
                LogToFile("FadeImage: CurrentImage or NextImage not found in Grid.");
                return;
            }

            // Set the new image source to the nextImage
            nextImage.Source = toSource;
            nextImage.Opacity = 0;

            // Fade out the current image and fade in the next image
            var fadeOutAnimation = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = TimeSpan.FromSeconds(FadeDurationSeconds)
            };
            var fadeInAnimation = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = TimeSpan.FromSeconds(FadeDurationSeconds)
            };

            fadeOutAnimation.Completed += (s, e) =>
            {
                // Swap the images
                currentImage.Source = toSource;
                currentImage.Opacity = 1;
                nextImage.Opacity = 0;
            };

            currentImage.BeginAnimation(System.Windows.Controls.Image.OpacityProperty, fadeOutAnimation);
            nextImage.BeginAnimation(System.Windows.Controls.Image.OpacityProperty, fadeInAnimation);
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

                // Update Monitor #2 with fade animation
                if (marqueeWindow != null && marqueeWindow.Content is Grid marqueeGrid)
                {
                    string marqueePath = game.MarqueePath ?? System.IO.Path.Combine(Program.InstallDir, "default_marquee.png");
                    FadeImage(marqueeGrid, new BitmapImage(new Uri(marqueePath, UriKind.Absolute)));
                }

                // Update Monitor #3 with fade animation (use Splash Screen image since Custom Controller Image isn't implemented)
                if (controllerWindow != null && controllerWindow.Content is Grid controllerGrid)
                {
                    string splashImagePath = null;
                    if (screenHeightPhysical >= 2160 && game.SplashScreenPath.ContainsKey("4k"))
                    {
                        splashImagePath = game.SplashScreenPath["4k"];
                    }
                    else if (screenHeightPhysical >= 1440 && game.SplashScreenPath.ContainsKey("1440p"))
                    {
                        splashImagePath = game.SplashScreenPath["1440p"];
                    }
                    else if (game.SplashScreenPath.ContainsKey("1080p"))
                    {
                        splashImagePath = game.SplashScreenPath["1080p"];
                    }

                    if (!string.IsNullOrEmpty(splashImagePath) && File.Exists(splashImagePath))
                    {
                        FadeImage(controllerGrid, new BitmapImage(new Uri(splashImagePath, UriKind.Absolute)));
                    }
                    else
                    {
                        string controllerDefaultPath = System.IO.Path.Combine(Program.InstallDir, "default_controller.png");
                        FadeImage(controllerGrid, new BitmapImage(new Uri(controllerDefaultPath, UriKind.Absolute)));
                    }
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

                // Launch the game immediately
                if (game.Type == "PC")
                {
                    Process gameProcess = new Process();
                    gameProcess.StartInfo.FileName = game.ExecutablePath;
                    gameProcess.StartInfo.UseShellExecute = true; // Required for proper process handling
                    gameProcess.EnableRaisingEvents = true;
                    isGameActive = true; // Set game active flag
                    gameProcess.Exited += (s, e) => Dispatcher.Invoke(() => PerformPostExitCleanup());
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
                        emulatorProcess.Exited += (s, e) => Dispatcher.Invoke(() => PerformPostExitCleanup());
                        emulatorProcess.Start();
                        activeProcess = emulatorProcess; // Set the active process
                        LogToFile($"Launched emulated game: {cmd}");
                    }
                    else
                    {
                        LogToFile($"Emulator plugin not found for game type: {game.EmulatorPlugin}");
                        throw new Exception($"Emulator plugin not found: {game.EmulatorPlugin}");
                    }
                }

                // Show the splash screen on Monitor 1 with a fade transition
                splashScreenWindow = new SplashScreenWindow(this, game, dpiScaleFactor, () =>
                {
                    // After the splash screen completes, ensure the game selection screen remains hidden
                    LogToFile("Splash screen completed during game launch. Game selection screen should remain hidden.");
                }, isLaunchPhase: true, startFadeTimer: false); // Launch phase, don't start fade timer yet

                // Fade out the game selection screen first
                var fadeOutAnimation = new DoubleAnimation
                {
                    From = 1.0,
                    To = 0.0,
                    Duration = TimeSpan.FromSeconds(FadeDurationSeconds / 2) // 0.3 seconds
                };
                fadeOutAnimation.Completed += (s, e) =>
                {
                    // Once the game selection screen is fully faded out, show the splash screen
                    splashScreenWindow.Opacity = 0; // Start with 0 opacity to prevent flash
                    splashScreenWindow.Visibility = Visibility.Visible;
                    splashScreenWindow.Show();
                    splashScreenWindow.Activate();
                    splashScreenWindow.Focus();
                    LogToFile($"SplashScreenWindow shown for launch phase. Visibility: {splashScreenWindow.Visibility}, Opacity: {splashScreenWindow.Opacity}");
                    // Ensure the splash screen is rendered before starting the fade-in
                    Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);
                    LogToFile("SplashScreenWindow render forced after Show() in launch phase.");
                    // Start the fade timer now to ensure a proper fade-in
                    splashScreenWindow.StartFadeTimer();
                    // Now hide the game selection screen completely
                    Visibility = Visibility.Hidden;
                };
                this.BeginAnimation(OpacityProperty, fadeOutAnimation);
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

                // Reset state and make the game selection screen visible again
                activeProcess = null;
                isGameActive = false;
                Visibility = Visibility.Visible;
                Topmost = true;
                Activate();
                Focus();

                // Use P/Invoke to ensure the window is the foreground window
                var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                SetForegroundWindow(handle);
                LogToFile($"Game selection screen restored (after launch failure) and set to foreground window with handle: {handle}");

                // Start a focus restoration loop to ensure focus is maintained
                StartFocusRestorationLoop();
            }
        }
    }
}