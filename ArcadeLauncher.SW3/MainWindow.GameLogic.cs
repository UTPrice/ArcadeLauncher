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

namespace ArcadeLauncher.SW3
{
    public partial class MainWindow
    {
        private void InitializeGameLogic()
        {
            // No timers needed for the minimal implementation
        }

        private List<IEmulatorPlugin> LoadPlugins()
        {
            // Get the directory of the executable
            string exePath = AppDomain.CurrentDomain.BaseDirectory;
            // Look for Plugins in the parent directory
            string pluginDir = Path.Combine(exePath, "..", "Plugins");
            pluginDir = Path.GetFullPath(pluginDir); // Resolve the path to avoid issues with relative paths
            var pluginList = new List<IEmulatorPlugin>();

            if (!Directory.Exists(pluginDir))
            {
                LogToFile($"Plugins directory not found: {pluginDir}");
                return pluginList;
            }

            foreach (var dll in Directory.GetFiles(pluginDir, "*.dll"))
            {
                try
                {
                    var assembly = System.Reflection.Assembly.LoadFrom(dll);
                    var types = assembly.GetTypes().Where(t => typeof(IEmulatorPlugin).IsAssignableFrom(t) && !t.IsInterface);
                    foreach (var type in types)
                    {
                        if (Activator.CreateInstance(type) is IEmulatorPlugin plugin)
                        {
                            pluginList.Add(plugin);
                            LogToFile($"Loaded plugin: {dll}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogToFile($"Failed to load plugin from {dll}: {ex.Message}");
                }
            }
            return pluginList;
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
                        });
                    };
                    gameProcess.Start();
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
                            });
                        };
                        emulatorProcess.Start();
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
            }
        }
    }
}