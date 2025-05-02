using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Diagnostics;

namespace ArcadeLauncher.SW3
{
    public partial class MainWindow
    {
        private const bool DebugXInputOverlay = true;
        private const int XInputPollingIntervalMs = 8;
        private const int XInputHoldThresholdMs = 500;
        private const int ContinuousMoveIntervalMs = 100;

        [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
        private static extern uint XInputGetState(uint dwUserIndex, ref XINPUT_STATE pState);

        [StructLayout(LayoutKind.Sequential)]
        private struct XINPUT_GAMEPAD
        {
            public ushort wButtons;
            public byte bLeftTrigger;
            public byte bRightTrigger;
            public short sThumbLX;
            public short sThumbLY;
            public short sThumbRX;
            public short sThumbRY;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XINPUT_STATE
        {
            public uint dwPacketNumber;
            public XINPUT_GAMEPAD Gamepad;
        }

        private const uint ERROR_SUCCESS = 0;
        private const uint ERROR_DEVICE_NOT_CONNECTED = 1167;
        private const short StickThreshold = 16384;

        private DispatcherTimer? xInputTimer;
        private List<bool> previousConnected;
        private List<(DateTime Up, DateTime Down, DateTime Left, DateTime Right)> pressStartTimes;
        private List<(DateTime Up, DateTime Down, DateTime Left, DateTime Right)> lastMoveTimes;
        private Canvas xInputOverlayCanvas;
        private TextBlock xInputOverlayText;
        private List<(bool Up, bool Down, bool Left, bool Right)> previousInputs;
        private PerformanceCounter cpuCounter;
        private int tickCount;
        private DateTime lastFpsUpdate;
        private double fps;
        // Track last log time for "Input ignored" per direction to throttle logs
        private List<(DateTime Up, DateTime Down, DateTime Left, DateTime Right)> lastIgnoreLogTimes;

        private void InitializeXInput()
        {
            LogToFile("[XInput] Initializing XInput polling...");
            previousConnected = new List<bool>(4);
            pressStartTimes = new List<(DateTime, DateTime, DateTime, DateTime)>(4);
            lastMoveTimes = new List<(DateTime, DateTime, DateTime, DateTime)>(4);
            previousInputs = new List<(bool Up, bool Down, bool Left, bool Right)>(4);
            lastIgnoreLogTimes = new List<(DateTime Up, DateTime Down, DateTime Left, DateTime Right)>(4);
            for (int i = 0; i < 4; i++)
            {
                previousConnected.Add(false);
                pressStartTimes.Add((DateTime.MinValue, DateTime.MinValue, DateTime.MinValue, DateTime.MinValue));
                lastMoveTimes.Add((DateTime.MinValue, DateTime.MinValue, DateTime.MinValue, DateTime.MinValue));
                previousInputs.Add((false, false, false, false));
                lastIgnoreLogTimes.Add((DateTime.MinValue, DateTime.MinValue, DateTime.MinValue, DateTime.MinValue));
            }

            xInputTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(XInputPollingIntervalMs)
            };
            xInputTimer.Tick += XInputTimer_Tick;
            xInputTimer.Start();
            LogToFile($"[XInput] Polling timer started at {XInputPollingIntervalMs}ms interval.");

            try
            {
                cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                cpuCounter.NextValue();
            }
            catch (Exception ex)
            {
                LogToFile($"[XInput] Failed to initialize CPU counter: {ex.Message}");
                cpuCounter = null;
            }

            tickCount = 0;
            lastFpsUpdate = DateTime.Now;
            fps = 0;

            SetupXInputOverlay();
        }

        private void SetupXInputOverlay()
        {
            xInputOverlayCanvas = new Canvas
            {
                Background = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0)),
                Width = 300,
                Height = 150,
                Visibility = Visibility.Hidden // Ensure the overlay starts hidden
            };
            Canvas.SetLeft(xInputOverlayCanvas, 10);
            Canvas.SetTop(xInputOverlayCanvas, 10);

            xInputOverlayText = new TextBlock
            {
                Foreground = Brushes.White,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            };
            Canvas.SetLeft(xInputOverlayText, 5);
            Canvas.SetTop(xInputOverlayText, 5);
            xInputOverlayCanvas.Children.Add(xInputOverlayText);

            canvas.Children.Add(xInputOverlayCanvas);
            LogToFile("[XInput] Debug overlay initialized with initial Visibility set to Hidden.");
        }

        private void XInputTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                var overlayText = new List<string>();
                for (uint i = 0; i < 4; i++)
                {
                    XINPUT_STATE state = new XINPUT_STATE();
                    uint result = XInputGetState(i, ref state);
                    bool isConnected = result == ERROR_SUCCESS;

                    if (isConnected != previousConnected[(int)i])
                    {
                        LogToFile($"[XInput] Controller {i}: {(isConnected ? "Connected" : "Disconnected")}");
                        previousConnected[(int)i] = isConnected;
                    }

                    if (!isConnected)
                    {
                        overlayText.Add($"C{i}: None");
                        continue;
                    }

                    short lx = state.Gamepad.sThumbLX;
                    short ly = state.Gamepad.sThumbLY;
                    var inputs = (Up: ly > StickThreshold, Down: ly < -StickThreshold, Left: lx < -StickThreshold, Right: lx > StickThreshold);

                    var prevInputs = previousInputs[(int)i];

                    if (inputs.Up != prevInputs.Up)
                    {
                        LogToFile($"[XInput] Controller {i}: Action=Up {(inputs.Up ? "pressed" : "released")}");
                    }
                    if (inputs.Down != prevInputs.Down)
                    {
                        LogToFile($"[XInput] Controller {i}: Action=Down {(inputs.Down ? "pressed" : "released")}");
                    }
                    if (inputs.Left != prevInputs.Left)
                    {
                        LogToFile($"[XInput] Controller {i}: Action=Left {(inputs.Left ? "pressed" : "released")}");
                    }
                    if (inputs.Right != prevInputs.Right)
                    {
                        LogToFile($"[XInput] Controller {i}: Action=Right {(inputs.Right ? "pressed" : "released")}");
                    }

                    previousInputs[(int)i] = inputs;

                    var currentStartTimes = pressStartTimes[(int)i];
                    var currentMoveTimes = lastMoveTimes[(int)i];

                    if (inputs.Up && currentStartTimes.Up == DateTime.MinValue)
                        currentStartTimes = (DateTime.Now, currentStartTimes.Down, currentStartTimes.Left, currentStartTimes.Right);
                    else if (!inputs.Up && currentStartTimes.Up != DateTime.MinValue)
                    {
                        currentStartTimes = (DateTime.MinValue, currentStartTimes.Down, currentStartTimes.Left, currentStartTimes.Right);
                        currentMoveTimes = (DateTime.MinValue, currentMoveTimes.Down, currentMoveTimes.Left, currentMoveTimes.Right);
                    }

                    if (inputs.Down && currentStartTimes.Down == DateTime.MinValue)
                        currentStartTimes = (currentStartTimes.Up, DateTime.Now, currentStartTimes.Left, currentStartTimes.Right);
                    else if (!inputs.Down && currentStartTimes.Down != DateTime.MinValue)
                    {
                        currentStartTimes = (currentStartTimes.Up, DateTime.MinValue, currentStartTimes.Left, currentStartTimes.Right);
                        currentMoveTimes = (currentMoveTimes.Up, DateTime.MinValue, currentMoveTimes.Left, currentMoveTimes.Right);
                    }

                    if (inputs.Left && currentStartTimes.Left == DateTime.MinValue)
                        currentStartTimes = (currentStartTimes.Up, currentStartTimes.Down, DateTime.Now, currentStartTimes.Right);
                    else if (!inputs.Left && currentStartTimes.Left != DateTime.MinValue)
                    {
                        currentStartTimes = (currentStartTimes.Up, currentStartTimes.Down, DateTime.MinValue, currentStartTimes.Right);
                        currentMoveTimes = (currentMoveTimes.Up, currentMoveTimes.Down, DateTime.MinValue, currentMoveTimes.Right);
                    }

                    if (inputs.Right && currentStartTimes.Right == DateTime.MinValue)
                        currentStartTimes = (currentStartTimes.Up, currentStartTimes.Down, currentStartTimes.Left, DateTime.Now);
                    else if (!inputs.Right && currentStartTimes.Right != DateTime.MinValue)
                    {
                        currentStartTimes = (currentStartTimes.Up, currentStartTimes.Down, currentStartTimes.Left, DateTime.MinValue);
                        currentMoveTimes = (currentMoveTimes.Up, currentMoveTimes.Down, currentMoveTimes.Left, DateTime.MinValue);
                    }

                    pressStartTimes[(int)i] = currentStartTimes;
                    lastMoveTimes[(int)i] = currentMoveTimes;

                    var actions = new List<string>();
                    if (inputs.Up) actions.Add("Up");
                    if (inputs.Down) actions.Add("Down");
                    if (inputs.Left) actions.Add("Left");
                    if (inputs.Right) actions.Add("Right");
                    overlayText.Add($"C{i}: {(actions.Count > 0 ? string.Join(", ", actions) : "None")}");
                }

                tickCount++;
                var now = DateTime.Now;
                var elapsed = (now - lastFpsUpdate).TotalSeconds;
                if (elapsed >= 1.0)
                {
                    fps = tickCount / elapsed;
                    tickCount = 0;
                    lastFpsUpdate = now;
                }

                float cpuUsage = cpuCounter != null ? cpuCounter.NextValue() : 0f;

                if (DebugXInputOverlay)
                {
                    overlayText.Add($"Polling: {(xInputTimer != null && xInputTimer.IsEnabled ? "Active" : "Stopped")}");
                    overlayText.Add($"CPU: {cpuUsage:F1}%");
                    overlayText.Add($"FPS: {fps:F1}");
                    xInputOverlayText.Text = string.Join("\n", overlayText);
                }
            }
            catch (Exception ex)
            {
                LogToFile($"[XInput] Error in XInputTimer_Tick: {ex.Message}");
            }
        }

        public List<(uint Controller, string Action, bool IsSingle)> GetXInputActions()
        {
            var actions = new List<(uint Controller, string Action, bool IsSingle)>();
            try
            {
                for (uint i = 0; i < 4; i++)
                {
                    XINPUT_STATE state = new XINPUT_STATE();
                    if (XInputGetState(i, ref state) != ERROR_SUCCESS)
                        continue;

                    DateTime now = DateTime.Now;
                    var startTimes = pressStartTimes[(int)i];
                    var moveTimes = lastMoveTimes[(int)i];
                    var lastLogTimes = lastIgnoreLogTimes[(int)i];
                    short lx = state.Gamepad.sThumbLX;
                    short ly = state.Gamepad.sThumbLY;
                    var inputs = (Up: ly > StickThreshold, Down: ly < -StickThreshold, Left: lx < -StickThreshold, Right: lx > StickThreshold);

                    if (inputs.Up && startTimes.Up != DateTime.MinValue)
                    {
                        double elapsedMs = (now - startTimes.Up).TotalMilliseconds;
                        double timeSinceLastMove = (now - moveTimes.Up).TotalMilliseconds;
                        if (elapsedMs < XInputHoldThresholdMs)
                        {
                            if (moveTimes.Up == DateTime.MinValue)
                            {
                                actions.Add((i, "Up", true));
                                moveTimes = (now, moveTimes.Down, moveTimes.Left, moveTimes.Right);
                                LogToFile($"[XInput] Controller {i}: Single move Up, held for {elapsedMs:F0}ms");
                            }
                            else
                            {
                                double timeSinceLastLog = (now - lastLogTimes.Up).TotalMilliseconds;
                                if (timeSinceLastLog >= 100) // Throttle to every 100ms
                                {
                                    LogToFile($"[XInput] Controller {i}: Input ignored (sThumbLX={lx}, sThumbLY={ly}, StartTimes: Up={startTimes.Up:HH:mm:ss.fff}, Down={startTimes.Down:HH:mm:ss.fff}, Left={startTimes.Left:HH:mm:ss.fff}, Right={startTimes.Right:HH:mm:ss.fff}, MoveTimes: Up={moveTimes.Up:HH:mm:ss.fff}, Down={moveTimes.Down:HH:mm:ss.fff}, Left={moveTimes.Left:HH:mm:ss.fff}, Right={moveTimes.Right:HH:mm:ss.fff}) at {DateTime.Now:HH:mm:ss.fff}");
                                    lastLogTimes = (now, lastLogTimes.Down, lastLogTimes.Left, lastLogTimes.Right);
                                }
                            }
                        }
                        else if (timeSinceLastMove >= ContinuousMoveIntervalMs)
                        {
                            actions.Add((i, "Up", false));
                            moveTimes = (now, moveTimes.Down, moveTimes.Left, moveTimes.Right);
                            LogToFile($"[XInput] Controller {i}: Continuous move Up, held for {elapsedMs:F0}ms");
                        }
                    }

                    if (inputs.Down && startTimes.Down != DateTime.MinValue)
                    {
                        double elapsedMs = (now - startTimes.Down).TotalMilliseconds;
                        double timeSinceLastMove = (now - moveTimes.Down).TotalMilliseconds;
                        if (elapsedMs < XInputHoldThresholdMs)
                        {
                            if (moveTimes.Down == DateTime.MinValue)
                            {
                                actions.Add((i, "Down", true));
                                moveTimes = (moveTimes.Up, now, moveTimes.Left, moveTimes.Right);
                                LogToFile($"[XInput] Controller {i}: Single move Down, held for {elapsedMs:F0}ms");
                            }
                            else
                            {
                                double timeSinceLastLog = (now - lastLogTimes.Down).TotalMilliseconds;
                                if (timeSinceLastLog >= 100) // Throttle to every 100ms
                                {
                                    LogToFile($"[XInput] Controller {i}: Input ignored (sThumbLX={lx}, sThumbLY={ly}, StartTimes: Up={startTimes.Up:HH:mm:ss.fff}, Down={startTimes.Down:HH:mm:ss.fff}, Left={startTimes.Left:HH:mm:ss.fff}, Right={startTimes.Right:HH:mm:ss.fff}, MoveTimes: Up={moveTimes.Up:HH:mm:ss.fff}, Down={moveTimes.Down:HH:mm:ss.fff}, Left={moveTimes.Left:HH:mm:ss.fff}, Right={moveTimes.Right:HH:mm:ss.fff}) at {DateTime.Now:HH:mm:ss.fff}");
                                    lastLogTimes = (lastLogTimes.Up, now, lastLogTimes.Left, lastLogTimes.Right);
                                }
                            }
                        }
                        else if (timeSinceLastMove >= ContinuousMoveIntervalMs)
                        {
                            actions.Add((i, "Down", false));
                            moveTimes = (moveTimes.Up, now, moveTimes.Left, moveTimes.Right);
                            LogToFile($"[XInput] Controller {i}: Continuous move Down, held for {elapsedMs:F0}ms");
                        }
                    }

                    if (inputs.Left && startTimes.Left != DateTime.MinValue)
                    {
                        double elapsedMs = (now - startTimes.Left).TotalMilliseconds;
                        double timeSinceLastMove = (now - moveTimes.Left).TotalMilliseconds;
                        if (elapsedMs < XInputHoldThresholdMs)
                        {
                            if (moveTimes.Left == DateTime.MinValue)
                            {
                                actions.Add((i, "Left", true));
                                moveTimes = (moveTimes.Up, moveTimes.Down, now, moveTimes.Right);
                                LogToFile($"[XInput] Controller {i}: Single move Left, held for {elapsedMs:F0}ms");
                            }
                            else
                            {
                                double timeSinceLastLog = (now - lastLogTimes.Left).TotalMilliseconds;
                                if (timeSinceLastLog >= 100) // Throttle to every 100ms
                                {
                                    LogToFile($"[XInput] Controller {i}: Input ignored (sThumbLX={lx}, sThumbLY={ly}, StartTimes: Up={startTimes.Up:HH:mm:ss.fff}, Down={startTimes.Down:HH:mm:ss.fff}, Left={startTimes.Left:HH:mm:ss.fff}, Right={startTimes.Right:HH:mm:ss.fff}, MoveTimes: Up={moveTimes.Up:HH:mm:ss.fff}, Down={moveTimes.Down:HH:mm:ss.fff}, Left={moveTimes.Left:HH:mm:ss.fff}, Right={moveTimes.Right:HH:mm:ss.fff}) at {DateTime.Now:HH:mm:ss.fff}");
                                    lastLogTimes = (lastLogTimes.Up, lastLogTimes.Down, now, lastLogTimes.Right);
                                }
                            }
                        }
                        else if (timeSinceLastMove >= ContinuousMoveIntervalMs)
                        {
                            actions.Add((i, "Left", false));
                            moveTimes = (moveTimes.Up, moveTimes.Down, now, moveTimes.Right);
                            LogToFile($"[XInput] Controller {i}: Continuous move Left, held for {elapsedMs:F0}ms");
                        }
                    }

                    if (inputs.Right && startTimes.Right != DateTime.MinValue)
                    {
                        double elapsedMs = (now - startTimes.Right).TotalMilliseconds;
                        double timeSinceLastMove = (now - moveTimes.Right).TotalMilliseconds;
                        if (elapsedMs < XInputHoldThresholdMs)
                        {
                            if (moveTimes.Right == DateTime.MinValue)
                            {
                                actions.Add((i, "Right", true));
                                moveTimes = (moveTimes.Up, moveTimes.Down, moveTimes.Left, now);
                                LogToFile($"[XInput] Controller {i}: Single move Right, held for {elapsedMs:F0}ms");
                            }
                            else
                            {
                                double timeSinceLastLog = (now - lastLogTimes.Right).TotalMilliseconds;
                                if (timeSinceLastLog >= 100) // Throttle to every 100ms
                                {
                                    LogToFile($"[XInput] Controller {i}: Input ignored (sThumbLX={lx}, sThumbLY={ly}, StartTimes: Up={startTimes.Up:HH:mm:ss.fff}, Down={startTimes.Down:HH:mm:ss.fff}, Left={startTimes.Left:HH:mm:ss.fff}, Right={startTimes.Right:HH:mm:ss.fff}, MoveTimes: Up={moveTimes.Up:HH:mm:ss.fff}, Down={moveTimes.Down:HH:mm:ss.fff}, Left={moveTimes.Left:HH:mm:ss.fff}, Right={moveTimes.Right:HH:mm:ss.fff}) at {DateTime.Now:HH:mm:ss.fff}");
                                    lastLogTimes = (lastLogTimes.Up, lastLogTimes.Down, lastLogTimes.Left, now);
                                }
                            }
                        }
                        else if (timeSinceLastMove >= ContinuousMoveIntervalMs)
                        {
                            actions.Add((i, "Right", false));
                            moveTimes = (moveTimes.Up, moveTimes.Down, moveTimes.Left, now);
                            LogToFile($"[XInput] Controller {i}: Continuous move Right, held for {elapsedMs:F0}ms");
                        }
                    }

                    lastMoveTimes[(int)i] = moveTimes;
                    lastIgnoreLogTimes[(int)i] = lastLogTimes;
                }

                foreach (var action in actions)
                {
                    LogToFile($"[XInput] Controller {action.Controller}: {(action.IsSingle ? "Single" : "Continuous")} MoveSelection: {action.Action} at {DateTime.Now:HH:mm:ss.fff}");
                }
            }
            catch (Exception ex)
            {
                LogToFile($"[XInput] Error in GetXInputActions: {ex.Message}");
            }
            return actions;
        }

        private void ShutdownXInput()
        {
            if (xInputTimer != null)
            {
                xInputTimer.Stop();
                xInputTimer.Tick -= XInputTimer_Tick;
                LogToFile("[XInput] Polling timer stopped.");
            }

            if (DebugXInputOverlay && xInputOverlayCanvas != null)
            {
                canvas.Children.Remove(xInputOverlayCanvas);
                LogToFile("[XInput] Debug overlay removed.");
            }

            cpuCounter?.Dispose();
        }
    }
}