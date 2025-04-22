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
        private Process? activeProcess;
        private DateTime? lastKillPressTime;
        private bool isGameActive;
        private const int DoublePressThreshold = 500;
        private IntPtr hookId = IntPtr.Zero;

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

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        private const uint PROCESS_TERMINATE = 0x0001;
        private const uint PROCESS_QUERY_INFORMATION = 0x0400;

        private HookProc hookProc;

        private void InitializeGameLogic()
        {
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
                LogToFile($"Global hook detected key down at {DateTime.Now:HH:mm:ss.fff}: {keyString}");

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
                            LogToFile($"Restored focus to MainWindow on key down at {DateTime.Now:HH:mm:ss.fff} (handle: {mainWindowHandle}), previous foreground window was: {currentForegroundWindow}");
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
                            Dispatcher.Invoke(() =>
                            {
                                LogToFile($"Global hook: Kill Switch double-press detected for key {keyString} at {DateTime.Now:HH:mm:ss.fff}. Active process state: {activeProcess?.HasExited ?? true}");
                                if (isGameActive && activeProcess != null && !activeProcess.HasExited)
                                {
                                    try
                                    {
                                        LogToFile($"Attempting to terminate process at {DateTime.Now:HH:mm:ss.fff}: {activeProcess.StartInfo.FileName}");
                                        activeProcess.Kill();
                                        LogToFile($"Process terminated at {DateTime.Now:HH:mm:ss.fff}: {activeProcess.StartInfo.FileName}");
                                        PerformPostExitCleanup();
                                        isGameActive = false;
                                    }
                                    catch (Exception ex)
                                    {
                                        LogToFile($"Failed to terminate process at {DateTime.Now:HH:mm:ss.fff}: {ex.Message}");
                                    }
                                }
                                else
                                {
                                    LogToFile($"Global hook: No active process to terminate or process already exited at {DateTime.Now:HH:mm:ss.fff}.");
                                }
                                lastKillPressTime = null;
                            });
                        }
                        else
                        {
                            lastKillPressTime = currentTime;
                            LogToFile($"Global hook: Kill Switch single press detected for key {keyString} at {DateTime.Now:HH:mm:ss.fff}. Waiting for second press within {DoublePressThreshold}ms.");
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
                        try
                        {
                            if (!activeProcess.HasExited)
                            {
                                KillProcessTree(activeProcess.Id);
                                LogToFile($"Terminated process tree for process ID: {activeProcess.Id} at {DateTime.Now:HH:mm:ss.fff}");
                            }
                        }
                        catch (Exception ex)
                        {
                            LogToFile($"Failed to terminate process tree at {DateTime.Now:HH:mm:ss.fff}: {ex.Message}");
                        }

                        if (splashScreenWindow != null)
                        {
                            splashScreenWindow.Close();
                            LogToFile($"Closed existing SplashScreenWindow before T3 at {DateTime.Now:HH:mm:ss.fff}.");
                            splashScreenWindow = null;
                        }

                        splashScreenWindow = new SplashScreenWindow(this, game, dpiScaleFactor, () =>
                        {
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

                            foreach (var cmd in game.PostExitCommands ?? new List<string>())
                            {
                                RunCommand(cmd, "Post-Exit Command");
                            }

                            var screenWidthLogicalPostExit = SystemParameters.PrimaryScreenWidth;
                            var screenHeightLogicalPostExit = SystemParameters.PrimaryScreenHeight;
                            var screenWidthPhysicalPostExit = (int)(screenWidthLogicalPostExit * dpiScaleFactor);
                            var screenHeightPhysicalPostExit = (int)(screenHeightLogicalPostExit * dpiScaleFactor);
                            System.Windows.Forms.Cursor.Position = new System.Drawing.Point(screenWidthPhysicalPostExit - 1, screenHeightPhysicalPostExit - 1);
                            LogToFile($"Restored mouse cursor after exit at {DateTime.Now:HH:mm:ss.fff}: Moved to bottom-right pixel (physical): ({screenWidthPhysicalPostExit - 1}, {screenHeightPhysicalPostExit - 1})");

                            activeProcess = null;
                            isGameActive = false;
                        }, isLaunchPhase: false, startFadeTimer: false);

                        Visibility = Visibility.Hidden;
                        Opacity = 0;
                        LogToFile($"MainWindow hidden for T3 black background at {DateTime.Now:HH:mm:ss.fff}. Visibility: {Visibility}, Opacity: {Opacity}");

                        splashScreenWindow.Opacity = 0;
                        splashScreenWindow.Visibility = Visibility.Visible;
                        splashScreenWindow.Show();
                        var splashHandle = new System.Windows.Interop.WindowInteropHelper(splashScreenWindow).Handle;
                        SetForegroundWindow(splashHandle);
                        splashScreenWindow.Activate();
                        splashScreenWindow.Focus();
                        LogToFile($"SplashScreenWindow shown for T3 exit phase at {DateTime.Now:HH:mm:ss.fff}. Visibility: {splashScreenWindow.Visibility}, Opacity: {splashScreenWindow.Opacity}, Handle: {splashHandle}");

                        Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
                        LogToFile($"SplashScreenWindow render forced for T3 at {DateTime.Now:HH:mm:ss.fff}.");

                        splashScreenWindow.StartFadeAnimation();

                        var focusTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
                        int focusAttempts = 0;
                        focusTimer.Tick += (s, e) =>
                        {
                            focusAttempts++;
                            var currentForeground = GetForegroundWindow();
                            if (currentForeground != splashHandle)
                            {
                                SetForegroundWindow(splashHandle);
                                LogToFile($"T3 focus attempt {focusAttempts} at {DateTime.Now:HH:mm:ss.fff}: SetForegroundWindow called for SplashScreenWindow, current foreground was {currentForeground}, target handle: {splashHandle}");
                            }
                            if (focusAttempts >= 20)
                            {
                                focusTimer.Stop();
                                LogToFile($"T3 focus loop stopped at {DateTime.Now:HH:mm:ss.fff} after {focusAttempts} attempts.");
                            }
                        };
                        focusTimer.Start();
                        LogToFile($"T3 focus loop started for SplashScreenWindow at {DateTime.Now:HH:mm:ss.fff}.");
                    }
                    else
                    {
                        LogToFile($"Could not determine game for post-exit cleanup at {DateTime.Now:HH:mm:ss.fff}.");
                        activeProcess = null;
                        isGameActive = false;
                        Visibility = Visibility.Visible;
                        Topmost = true;
                        Activate();
                        Focus();

                        var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                        SetForegroundWindow(handle);
                        LogToFile($"Game selection screen restored (no game found) and set to foreground window with handle: {handle} at {DateTime.Now:HH:mm:ss.fff}");

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
                    SetForegroundWindow(handle);
                    LogToFile($"Focus restoration attempt {focusAttempts} at {DateTime.Now:HH:mm:ss.fff}: SetForegroundWindow called, current foreground window was {currentForegroundWindow}, target handle: {handle}");
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
                gameProcess.WaitForInputIdle(1000);
                handle = gameProcess.MainWindowHandle;
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
                    SetForegroundWindow(handle);
                    LogToFile($"Game focus attempt {focusAttempts} at {DateTime.Now:HH:mm:ss.fff}: SetForegroundWindow called for game process, current foreground was {currentForeground}, target handle: {handle}");
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

        private void KillProcessTree(int processId)
        {
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

            var processes = Process.GetProcesses();
            foreach (var process in processes)
            {
                try
                {
                    if (GetParentProcessId(process.Id) == processId)
                    {
                        KillProcessTree(process.Id);
                    }
                }
                catch (Exception ex)
                {
                    LogToFile($"Error while killing child process {process.Id} at {DateTime.Now:HH:mm:ss.fff}: {ex.Message}");
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
                    return process.Id;
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

            if (delta == 1)
            {
                int currentCol = SelectedIndex % columns;
                if (currentCol == columns - 1)
                {
                    return;
                }
            }
            else if (delta == -1)
            {
                int currentCol = SelectedIndex % columns;
                if (currentCol == 0)
                {
                    return;
                }
            }

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
                    selectedItem.Focus();
                    ScrollToSelectedItem();
                    LogToFile($"Moved to PictureBox {SelectedIndex} at {DateTime.Now:HH:mm:ss.fff}: Row={SelectedIndex / columns}, Col={SelectedIndex % columns}");
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
        }

        private void RunCommand(string command, string description)
        {
            try
            {
                Process.Start("cmd.exe", $"/c {command}");
                LogToFile($"Successfully executed {description} at {DateTime.Now:HH:mm:ss.fff}: {command}");
            }
            catch (Exception ex)
            {
                LogToFile($"Failed to execute {description} at {DateTime.Now:HH:mm:ss.fff}: {command}, Error: {ex.Message}");
            }
        }

        private void FadeImage(Grid grid, ImageSource toSource)
        {
            var currentImage = grid.Children.OfType<System.Windows.Controls.Image>().FirstOrDefault(i => i.Name == "CurrentImage");
            var nextImage = grid.Children.OfType<System.Windows.Controls.Image>().FirstOrDefault(i => i.Name == "NextImage");

            if (currentImage == null || nextImage == null)
            {
                LogToFile($"FadeImage: CurrentImage or NextImage not found in Grid at {DateTime.Now:HH:mm:ss.fff}.");
                return;
            }

            nextImage.Source = toSource;
            nextImage.Opacity = 0;

            var fadeOutAnimation = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = TimeSpan.FromSeconds(settings.FadeDuration)
            };
            var fadeInAnimation = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = TimeSpan.FromSeconds(settings.FadeDuration)
            };

            fadeOutAnimation.Completed += (s, e) =>
            {
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
                LogToFile($"Launching game at {DateTime.Now:HH:mm:ss.fff}: {game.DisplayName}, HideMouseCursor: {game.HideMouseCursor}");

                var screenWidthLogical = SystemParameters.PrimaryScreenWidth;
                var screenHeightLogical = SystemParameters.PrimaryScreenHeight;
                var screenWidthPhysical = (int)(screenWidthLogical * dpiScaleFactor);
                var screenHeightPhysical = (int)(screenHeightLogical * dpiScaleFactor);

                if (game.HideMouseCursor)
                {
                    LogToFile($"HideMouseCursor is true, leaving cursor at bottom-right pixel (physical): ({screenWidthPhysical - 1}, {screenHeightPhysical - 1}) at {DateTime.Now:HH:mm:ss.fff}");
                }
                else
                {
                    int centerX = screenWidthPhysical / 2;
                    int centerY = screenHeightPhysical / 2;
                    System.Windows.Forms.Cursor.Position = new System.Drawing.Point(centerX, centerY);
                    LogToFile($"HideMouseCursor is false, moved cursor to center of screen (physical): ({centerX}, {centerY}) at {DateTime.Now:HH:mm:ss.fff}");
                }

                if (marqueeWindow != null && marqueeWindow.Content is Grid marqueeGrid)
                {
                    string marqueePath = game.MarqueePath ?? System.IO.Path.Combine(Program.InstallDir, "default_marquee.png");
                    FadeImage(marqueeGrid, new BitmapImage(new Uri(marqueePath, UriKind.Absolute)));
                }

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

                foreach (var cmd in game.PreLaunchCommands ?? new List<string>())
                {
                    RunCommand(cmd, "Pre-Launch Command");
                }

                if (!string.IsNullOrEmpty(game.LEDBlinkyCommand))
                {
                    RunCommand(game.LEDBlinkyCommand, "LEDBlinky Command");
                }

                if (splashScreenWindow != null)
                {
                    splashScreenWindow.Close();
                    LogToFile($"Closed existing SplashScreenWindow before new launch at {DateTime.Now:HH:mm:ss.fff}.");
                    splashScreenWindow = null;
                }

                splashScreenWindow = new SplashScreenWindow(this, game, dpiScaleFactor, () =>
                {
                    LogToFile($"Splash screen completed during game launch at {DateTime.Now:HH:mm:ss.fff}. Game selection screen should remain hidden.");
                    if (activeProcess != null && !activeProcess.HasExited)
                    {
                        StartGameFocusLoop(activeProcess);
                    }
                    if (splashScreenWindow != null)
                    {
                        splashScreenWindow.Close();
                        LogToFile($"Closed SplashScreenWindow in onComplete at {DateTime.Now:HH:mm:ss.fff}.");
                        splashScreenWindow = null;
                    }
                }, isLaunchPhase: true, startFadeTimer: false);

                var fadeOutAnimation = new DoubleAnimation
                {
                    From = 1.0,
                    To = 0.0,
                    Duration = TimeSpan.FromSeconds(FadeDurationSeconds / 2)
                };
                fadeOutAnimation.Completed += (s, e) =>
                {
                    splashScreenWindow.Opacity = 0;
                    splashScreenWindow.Visibility = Visibility.Visible;
                    splashScreenWindow.Show();
                    var splashHandle = new System.Windows.Interop.WindowInteropHelper(splashScreenWindow).Handle;
                    SetForegroundWindow(splashHandle);
                    splashScreenWindow.Activate();
                    splashScreenWindow.Focus();
                    LogToFile($"SplashScreenWindow shown for launch phase at {DateTime.Now:HH:mm:ss.fff}. Visibility: {splashScreenWindow.Visibility}, Opacity: {splashScreenWindow.Opacity}, Handle: {splashHandle}");
                    Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);
                    LogToFile($"SplashScreenWindow render forced after Show() in launch phase at {DateTime.Now:HH:mm:ss.fff}.");
                    splashScreenWindow.StartFadeTimer();
                    Visibility = Visibility.Hidden;
                };
                this.BeginAnimation(OpacityProperty, fadeOutAnimation);

                if (game.Type == "PC")
                {
                    Process gameProcess = new Process();
                    gameProcess.StartInfo.FileName = game.ExecutablePath;
                    gameProcess.StartInfo.UseShellExecute = true;
                    gameProcess.EnableRaisingEvents = true;
                    isGameActive = true;
                    gameProcess.Exited += (s, e) => Dispatcher.Invoke(() => PerformPostExitCleanup());
                    gameProcess.Start();
                    activeProcess = gameProcess;
                    LogToFile($"Launched PC game at {DateTime.Now:HH:mm:ss.fff}: {game.ExecutablePath}");
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
                        isGameActive = true;
                        emulatorProcess.Exited += (s, e) => Dispatcher.Invoke(() => PerformPostExitCleanup());
                        emulatorProcess.Start();
                        activeProcess = emulatorProcess;
                        LogToFile($"Launched emulated game at {DateTime.Now:HH:mm:ss.fff}: {cmd}");
                    }
                    else
                    {
                        LogToFile($"Emulator plugin not found for game type: {game.EmulatorPlugin} at {DateTime.Now:HH:mm:ss.fff}");
                        throw new Exception($"Emulator plugin not found: {game.EmulatorPlugin}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogToFile($"Failed to launch game at {DateTime.Now:HH:mm:ss.fff}: {ex.Message}");
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

                var screenWidthLogicalPostFailure = SystemParameters.PrimaryScreenWidth;
                var screenHeightLogicalPostFailure = SystemParameters.PrimaryScreenHeight;
                var screenWidthPhysicalPostFailure = (int)(screenWidthLogicalPostFailure * dpiScaleFactor);
                var screenHeightPhysicalPostFailure = (int)(screenHeightLogicalPostFailure * dpiScaleFactor);
                System.Windows.Forms.Cursor.Position = new System.Drawing.Point(screenWidthPhysicalPostFailure - 1, screenHeightPhysicalPostFailure - 1);
                LogToFile($"Restored mouse cursor after launch failure at {DateTime.Now:HH:mm:ss.fff}: Moved to bottom-right pixel (physical): ({screenWidthPhysicalPostFailure - 1}, {screenHeightPhysicalPostFailure - 1})");

                activeProcess = null;
                isGameActive = false;
                Visibility = Visibility.Visible;
                Topmost = true;
                Activate();
                Focus();

                var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                SetForegroundWindow(handle);
                LogToFile($"Game selection screen restored (after launch failure) and set to foreground window with handle: {handle} at {DateTime.Now:HH:mm:ss.fff}");

                StartFocusRestorationLoop();
            }
        }
    }
}