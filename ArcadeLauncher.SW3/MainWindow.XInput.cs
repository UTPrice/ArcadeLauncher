using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace ArcadeLauncher.SW3
{
    public partial class MainWindow
    {
        // Configuration flags
        private const bool DebugXInputOverlay = true; // Toggle debug overlay
        private const int XInputPollingIntervalMs = 16; // 60Hz (~16.67ms)

        // XInput P/Invoke
        [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
        private static extern uint XInputGetState(uint dwUserIndex, ref XINPUT_STATE pState);

        // XInput structs
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

        // Constants
        private const uint ERROR_SUCCESS = 0;
        private const uint ERROR_DEVICE_NOT_CONNECTED = 1167;
        private const short StickThreshold = 16384; // Half max (32767) for digital detection

        // State tracking
        private DispatcherTimer xInputTimer;
        private List<(bool Up, bool Down, bool Left, bool Right)> previousInputs;
        private List<bool> previousConnected; // Track connection state
        private Canvas xInputOverlayCanvas;
        private TextBlock xInputOverlayText;

        private void InitializeXInput()
        {
            LogToFile("[XInput] Initializing XInput polling...");
            previousInputs = new List<(bool, bool, bool, bool)>(4);
            previousConnected = new List<bool>(4);
            for (int i = 0; i < 4; i++)
            {
                previousInputs.Add((false, false, false, false));
                previousConnected.Add(false); // Initially assume disconnected
            }

            xInputTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(XInputPollingIntervalMs)
            };
            xInputTimer.Tick += XInputTimer_Tick;
            xInputTimer.Start();
            LogToFile($"[XInput] Polling timer started at {XInputPollingIntervalMs}ms interval.");

            SetupXInputOverlay();
        }

        private void SetupXInputOverlay()
        {
            if (!DebugXInputOverlay) return;

            xInputOverlayCanvas = new Canvas
            {
                Background = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0)), // Semi-transparent black
                Width = 300,
                Height = 100
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

            // Add to main canvas (set in MainWindow.xaml.cs)
            canvas.Children.Add(xInputOverlayCanvas);
            LogToFile("[XInput] Debug overlay initialized.");
        }

        private void XInputTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                var overlayText = new List<string>();
                for (uint i = 0; i < 4; i++)
                {
                    XINPUT_STATE state = new XINPUT_STATE();
                    uint result = XInputGetState(i, ref state);
                    bool isConnected = result == ERROR_SUCCESS;

                    // Log connection state changes
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
                    var previous = previousInputs[(int)i];

                    // Log state changes
                    if (inputs.Up != previous.Up)
                        LogToFile($"[XInput] Controller {i}: sThumbLY={ly}, Action=Up {(inputs.Up ? "pressed" : "released")}");
                    if (inputs.Down != previous.Down)
                        LogToFile($"[XInput] Controller {i}: sThumbLY={ly}, Action=Down {(inputs.Down ? "pressed" : "released")}");
                    if (inputs.Left != previous.Left)
                        LogToFile($"[XInput] Controller {i}: sThumbLX={lx}, Action=Left {(inputs.Left ? "pressed" : "released")}");
                    if (inputs.Right != previous.Right)
                        LogToFile($"[XInput] Controller {i}: sThumbLX={lx}, Action=Right {(inputs.Right ? "pressed" : "released")}");

                    // Update previous state
                    previousInputs[(int)i] = inputs;

                    // Build overlay text
                    var actions = new List<string>();
                    if (inputs.Up) actions.Add("Up");
                    if (inputs.Down) actions.Add("Down");
                    if (inputs.Left) actions.Add("Left");
                    if (inputs.Right) actions.Add("Right");
                    overlayText.Add($"C{i}: {(actions.Count > 0 ? string.Join(", ", actions) : "None")}");
                }

                if (DebugXInputOverlay)
                {
                    xInputOverlayText.Text = string.Join("\n", overlayText);
                }
            }
            catch (Exception ex)
            {
                LogToFile($"[XInput] Error in XInputTimer_Tick: {ex.Message}");
            }
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
        }
    }
}