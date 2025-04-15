using ArcadeLauncher.Core;
using ArcadeLauncher.Plugins;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Diagnostics; // For Debug.WriteLine

namespace ArcadeLauncher.SW1
{
    public partial class MainForm : Form
    {
        private List<Game> games;
        private Settings settings;
        private List<IEmulatorPlugin> plugins;
        private int selectedIndex = 0;
        private TableLayoutPanel gamePanel;
        private Panel gamePanelContainer;
        private Form marqueeForm;
        private Form controllerForm;
        private Image perimeterShadow; // The 10-pixel perimeter image with drop shadow
        private float dpiScaleFactor; // DPI scaling factor (e.g., 1.25 for 125% scaling)
        private List<Image> preloadedImages; // Store preloaded composite images

        // Smooth scrolling variables
        private Timer discreteScrollTimer; // For discrete scrolling animations
        private Timer continuousScrollTimer; // For continuous scrolling when holding the key
        private Timer holdDetectionTimer; // To detect if the key is held for continuous scrolling
        private int targetScrollY; // Target Y position for discrete scrolling
        private int currentScrollY; // Current Y position during animation
        private int scrollStep; // Pixels to move per tick during discrete scrolling
        private const int DiscreteAnimationDurationMs = 200; // 200 ms for discrete scrolling animation
        private const int DiscreteTimerIntervalMs = 10; // Update every 10 ms for discrete scrolling
        private const int ContinuousScrollSpeed = 2000; // Pixels per second for continuous scrolling
        private const int ContinuousTimerIntervalMs = 10; // Update every 20 ms for continuous scrolling
        private const int HoldDetectionDelayMs = 300; // Delay to detect a hold (300 ms)
        private bool isScrolling; // Flag to track if a discrete scroll animation is in progress
        private bool isHoldingUp; // Track if the Up key is being held
        private bool isHoldingDown; // Track if the Down key is being held
        private int continuousScrollDirection; // 1 for down, -1 for up, 0 for stopped
        private bool isContinuousScrolling; // Track if continuous scrolling is active

        // Custom PictureBox class for basic rendering
        private class ShadowPictureBox : PictureBox
        {
            private readonly int shadowIndex;
            private readonly MainForm parentForm;

            public ShadowPictureBox(int index, MainForm parent)
            {
                this.shadowIndex = index;
                this.parentForm = parent;
                // Remove any default border
                this.BorderStyle = BorderStyle.None;
                // Set a dark background to match typical cover art edges
                this.BackColor = Color.Black;
            }

            protected override void OnPaint(PaintEventArgs pe)
            {
                // Call the base OnPaint to draw the pre-rendered composite image
                base.OnPaint(pe);

                // Draw the highlight for the selected game, adjusted for the art box area
                if (shadowIndex == parentForm.selectedIndex)
                {
                    using (var path = new GraphicsPath())
                    {
                        // Adjust the highlight to fit the art box area (not the perimeter shadow)
                        int artBoxWidth = this.Width - 20; // Subtract 10 pixels on each side
                        int artBoxHeight = this.Height - 20; // Subtract 10 pixels on each side
                        path.AddRectangle(new Rectangle(10, 10, artBoxWidth - 1, artBoxHeight - 1)); // Offset by 10 pixels
                        using (var pen = new Pen(Color.FromArgb(100, 255, 255, 255), 5))
                        {
                            pe.Graphics.DrawPath(pen, path);
                        }
                    }
                }
            }
        }

        public MainForm()
        {
            this.AutoScaleMode = AutoScaleMode.Dpi; // Ensure DPI scaling is applied
            SetupComponents();
            LoadData();

            // Initialize smooth scrolling timers
            discreteScrollTimer = new Timer();
            discreteScrollTimer.Interval = DiscreteTimerIntervalMs;
            discreteScrollTimer.Tick += DiscreteScrollTimer_Tick;
            isScrolling = false;

            continuousScrollTimer = new Timer();
            continuousScrollTimer.Interval = ContinuousTimerIntervalMs;
            continuousScrollTimer.Tick += ContinuousScrollTimer_Tick;
            isHoldingUp = false;
            isHoldingDown = false;
            continuousScrollDirection = 0;
            isContinuousScrolling = false;

            holdDetectionTimer = new Timer();
            holdDetectionTimer.Interval = HoldDetectionDelayMs;
            holdDetectionTimer.Tick += HoldDetectionTimer_Tick;

            // Calculate the DPI scaling factor
            using (Graphics g = this.CreateGraphics())
            {
                dpiScaleFactor = g.DpiX / 96f; // Default DPI at 100% scaling is 96
                LogToFile($"DPI Scaling Factor: {dpiScaleFactor} (DPI: {g.DpiX})");
            }

            this.Load += (s, e) =>
            {
                // Ensure the form is maximized and ClientSize is correct
                this.FormBorderStyle = FormBorderStyle.None;
                this.WindowState = FormWindowState.Maximized;
                SetupUI();

                // Move the mouse cursor to the bottom-right pixel of the primary screen
                var screenBounds = Screen.PrimaryScreen.Bounds;
                Cursor.Position = new Point(screenBounds.Width - 1, screenBounds.Height - 1);
                LogToFile($"Moved cursor to bottom-right pixel: ({screenBounds.Width - 1}, {screenBounds.Height - 1})");

                // Log the actual PictureBox bounds and positions after rendering
                if (gamePanel.Controls.Count > 0)
                {
                    var firstPictureBox = gamePanel.Controls[0] as PictureBox;
                    if (firstPictureBox != null)
                    {
                        LogToFile($"First PictureBox Actual Bounds: Width={firstPictureBox.Bounds.Width}, Height={firstPictureBox.Bounds.Height}, X={firstPictureBox.Bounds.X}, Y={firstPictureBox.Bounds.Y}");
                    }
                    if (gamePanel.Controls.Count > 7) // Second row (index 7)
                    {
                        var secondRowPictureBox = gamePanel.Controls[7] as PictureBox;
                        if (secondRowPictureBox != null)
                        {
                            LogToFile($"Second Row PictureBox Actual Bounds: Width={secondRowPictureBox.Bounds.Width}, Height={secondRowPictureBox.Bounds.Height}, X={secondRowPictureBox.Bounds.X}, Y={secondRowPictureBox.Bounds.Y}");
                        }
                    }
                }
            };
            SetupMonitors();
            SetupInputHandling();
            this.DoubleBuffered = true; // Reduce flickering
            this.KeyPreview = true; // Ensure the form receives key events
        }

        private void SetupComponents()
        {
            this.BackColor = ColorTranslator.FromHtml("#555555"); // Match the shadow perimeter background
            this.Padding = new Padding(0); // Remove any form padding

            // Create a container panel for the TableLayoutPanel
            gamePanelContainer = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = false, // Disable AutoScroll to prevent scrollbars
                BackColor = ColorTranslator.FromHtml("#555555"), // Match the shadow perimeter background
                Margin = new Padding(0),
                Padding = new Padding(0)
            };

            gamePanel = new TableLayoutPanel
            {
                AutoSize = false, // Disable AutoSize to prevent shrinking
                AutoScroll = false, // Disable AutoScroll on the TableLayoutPanel
                BackColor = ColorTranslator.FromHtml("#555555"), // Match the shadow perimeter background
                Margin = new Padding(0),
                Padding = new Padding(0), // Ensure no extra padding
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None // Eliminate any border spacing
            };

            gamePanelContainer.Controls.Add(gamePanel);
            this.Controls.Add(gamePanelContainer);

            // Load the perimeter shadow image from a file
            string perimeterPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PerimeterShadow.png");
            if (File.Exists(perimeterPath))
            {
                perimeterShadow = Image.FromFile(perimeterPath);
            }
            else
            {
                LogToFile($"PerimeterShadow.png not found at {perimeterPath}");
                throw new FileNotFoundException("PerimeterShadow.png not found in the output directory.");
            }
        }

        private void LoadData()
        {
            games = DataManager.LoadGameData().Games.OrderBy(g => g.AlphabetizeName).ToList();
            settings = DataManager.LoadSettings();
            plugins = LoadPlugins();
        }

        private List<IEmulatorPlugin> LoadPlugins()
        {
            var pluginDir = Path.Combine(Program.InstallDir, "Plugins");
            Directory.CreateDirectory(pluginDir);
            var pluginList = new List<IEmulatorPlugin>();

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
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to load plugin from {dll}: {ex.Message}");
                }
            }
            return pluginList;
        }

        private void LogToFile(string message)
        {
            try
            {
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string logDir = Path.Combine(appDataPath, "ArcadeLauncher");
                Directory.CreateDirectory(logDir);
                string logFile = Path.Combine(logDir, "SW1_Log.txt");
                File.AppendAllText(logFile, $"{DateTime.Now}: {message}\n");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to log: {ex.Message}");
            }
        }

        private void SetupUI()
        {
            int columns = settings.NumberOfColumns > 0 ? settings.NumberOfColumns : 7; // Default to 7 columns if not set

            // Step 1: Get the screen width and convert to base logical pixels (96 DPI)
            int totalWidth = Screen.PrimaryScreen.Bounds.Width; // Logical pixels at current DPI (125% scaling)
            int baseWidth = (int)((float)totalWidth / dpiScaleFactor); // Convert to logical pixels at 96 DPI
            LogToFile($"Step 1 - Screen Width: totalWidth={totalWidth}, dpiScaleFactor={dpiScaleFactor}, baseWidth={baseWidth}");
            LogToFile($"ClientSize.Width (for reference): {this.ClientSize.Width}");

            // Step 2: Allocate percentages using the base width (logical pixels at 96 DPI)
            double combinedMarginPercentage = 0.0275; // 2.75% for left and right margins combined
            int baseCombinedMarginWidth = (int)(baseWidth * combinedMarginPercentage);
            int baseMarginWidth = baseCombinedMarginWidth / 2; // Left and right margins are equal
            int baseTopMargin = baseMarginWidth; // Top margin equals left margin
            int baseVerticalGap = (int)(baseTopMargin * 0.6); // Reduce vertical gap by 40% (from topMargin)

            int baseAvailableWidth = baseWidth - (baseMarginWidth * 2);

            double targetArtBoxPercentage = 0.80; // 80%
            double targetGapPercentage = 0.1725; // 17.25%
            int baseTargetTotalArtBoxWidth = (int)(baseWidth * targetArtBoxPercentage);
            int baseTargetTotalGapWidth = (int)(baseWidth * targetGapPercentage);

            int baseBoxWidth = baseTargetTotalArtBoxWidth / columns; // Total art box width divided by number of columns
            int baseGap = (columns > 1) ? baseTargetTotalGapWidth / (columns - 1) : 0; // Total gap width divided by number of gaps

            // Recalculate to ensure exact fit (distribute any remaining pixels across the gaps)
            int baseTotalArtBoxesWidth = baseBoxWidth * columns;
            int baseTotalGapsWidth = baseGap * (columns - 1);
            int baseTotalCalculatedWidth = baseTotalArtBoxesWidth + baseTotalGapsWidth + (baseMarginWidth * 2);
            int baseWidthDifference = baseWidth - baseTotalCalculatedWidth;
            if (baseWidthDifference != 0 && (columns - 1) > 0)
            {
                int gapAdjustment = baseWidthDifference / (columns - 1);
                baseGap += gapAdjustment;
                baseTotalGapsWidth = baseGap * (columns - 1);
            }

            // Ensure gap is non-negative
            if (baseGap < 0)
            {
                LogToFile($"Base Gap was negative ({baseGap}), setting to 0");
                baseGap = 0;
                baseTotalGapsWidth = 0;
            }

            int baseBoxHeight = (int)(baseBoxWidth * (4.0 / 3.0)); // 4:3 aspect ratio

            LogToFile($"Step 2 - Base Allocations (96 DPI): baseCombinedMarginWidth={baseCombinedMarginWidth}, baseMarginWidth={baseMarginWidth}, baseTopMargin={baseTopMargin}");
            LogToFile($"baseAvailableWidth={baseAvailableWidth}, baseBoxWidth={baseBoxWidth}, baseBoxHeight={baseBoxHeight}");
            LogToFile($"baseTotalGapsWidth={baseTotalGapsWidth}, baseGap={baseGap}, baseVerticalGap={baseVerticalGap}");

            // Step 3: Use the base measurements directly (let the TableLayoutPanel apply DPI scaling)
            int marginWidth = baseMarginWidth;
            int topMargin = baseTopMargin;
            int verticalGap = baseVerticalGap;
            int boxWidth = baseBoxWidth;
            int boxHeight = baseBoxHeight;
            int gap = baseGap;

            int totalArtBoxesWidth = boxWidth * columns;
            int totalGapsWidth = gap * (columns - 1);
            int totalCalculatedWidth = totalArtBoxesWidth + totalGapsWidth + (marginWidth * 2);
            int widthDifference = totalWidth - totalCalculatedWidth;
            if (widthDifference != 0 && columns > 0)
            {
                int boxWidthAdjustment = widthDifference / columns;
                boxWidth += boxWidthAdjustment;
                totalArtBoxesWidth = boxWidth * columns;
                totalCalculatedWidth = totalArtBoxesWidth + totalGapsWidth + (marginWidth * 2);
                // Recalculate boxHeight to maintain 4:3 aspect ratio
                boxHeight = (int)(boxWidth * (4.0 / 3.0));
            }

            int availableWidth = totalWidth - (marginWidth * 2);

            LogToFile($"Step 3 - Final Allocations (Passed to TableLayoutPanel): marginWidth={marginWidth}, topMargin={topMargin}, verticalGap={verticalGap}");
            LogToFile($"availableWidth={availableWidth}, boxWidth={boxWidth}, boxHeight={boxHeight}");
            LogToFile($"totalArtBoxesWidth={totalArtBoxesWidth}, totalGapsWidth={totalGapsWidth}, gap={gap}");
            LogToFile($"totalCalculatedWidth={totalCalculatedWidth}, widthDifference={widthDifference}");

            // Calculate the number of rows needed
            int rows = (int)Math.Ceiling((double)games.Count / columns);

            // Preload all images
            preloadedImages = new List<Image>(games.Count);
            LogToFile($"Preloading images for {games.Count} games...");
            for (int i = 0; i < games.Count; i++)
            {
                Image compositeImage = null;
                if (File.Exists(games[i].ArtBoxPath))
                {
                    using (Image coverImage = Image.FromFile(games[i].ArtBoxPath))
                    {
                        compositeImage = CreateCompositeImage(coverImage, boxWidth, boxHeight);
                    }
                }
                preloadedImages.Add(compositeImage); // Add null if the image couldn't be loaded
            }
            LogToFile($"Finished preloading images.");

            // Set up the TableLayoutPanel
            gamePanel.Controls.Clear();
            gamePanel.ColumnStyles.Clear();
            gamePanel.RowStyles.Clear();
            gamePanel.ColumnCount = (columns * 2); // Art box columns + gap columns + left margin column
            gamePanel.RowCount = (rows * 2); // Art box rows + vertical gap rows + top margin row
            gamePanel.Width = totalWidth; // Explicitly set the width to match the screen
            gamePanel.Height = topMargin + (rows * (boxHeight + verticalGap)); // Explicitly set the height to accommodate all rows

            // Log the dimensions of the TableLayoutPanel and gamePanelContainer
            LogToFile($"TableLayoutPanel Dimensions: Width={gamePanel.Width}, Height={gamePanel.Height}");
            LogToFile($"gamePanelContainer ClientSize: Width={gamePanelContainer.ClientSize.Width}, Height={gamePanelContainer.ClientSize.Height}");

            // Define column styles (left margin + art box width and gaps)
            gamePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, marginWidth)); // Left margin column
            for (int col = 0; col < columns; col++)
            {
                gamePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, boxWidth));
                if (col < columns - 1) // Add a gap column between art boxes
                {
                    gamePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, gap));
                }
            }
            gamePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, marginWidth)); // Right margin column

            // Define row styles (top margin + art box height and vertical gaps)
            gamePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, topMargin)); // Top margin row
            for (int row = 0; row < rows; row++)
            {
                gamePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, boxHeight));
                if (row < rows - 1) // Add a vertical gap row between art box rows
                {
                    gamePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, verticalGap));
                }
            }

            // Add art boxes to the TableLayoutPanel
            for (int i = 0; i < games.Count; i++)
            {
                var game = games[i];
                int row = i / columns;
                int col = i % columns;

                // Calculate the column index in the TableLayoutPanel (accounting for left margin and gap columns)
                int colIndex = 1 + (col * 2); // Offset by 1 for the left margin column
                // Calculate the row index (accounting for top margin and vertical gap rows)
                int rowIndex = 1 + (row * 2); // Offset by 1 for the top margin row

                var pictureBox = new ShadowPictureBox(i, this)
                {
                    Size = new Size(boxWidth, boxHeight),
                    Margin = new Padding(0), // No additional margins, handled by TableLayoutPanel
                    SizeMode = PictureBoxSizeMode.StretchImage, // Stretch to fit exactly
                    Tag = i,
                    BackColor = Color.Black // Match typical dark edges of cover art
                };

                // Log PictureBox creation
                LogToFile($"Step 4 - Creating PictureBox {i}: Size={boxWidth}x{boxHeight}, Row={rowIndex}, Col={colIndex}");

                // Assign the preloaded image
                if (preloadedImages[i] != null)
                {
                    pictureBox.Image = preloadedImages[i];
                }
                else
                {
                    // Placeholder if no image is available
                    pictureBox.BackColor = Color.Gray;
                    pictureBox.Paint += (s, e) =>
                    {
                        using (var font = new Font("Arial", 8))
                        using (var brush = new SolidBrush(Color.White))
                        {
                            e.Graphics.DrawString("No Image", font, brush, new PointF(20, 60));
                        }
                    };
                }

                pictureBox.Click += (s, e) => LaunchGame(game);

                // Add the PictureBox to the TableLayoutPanel
                gamePanel.Controls.Add(pictureBox, colIndex, rowIndex);
            }

            // Ensure the selected game is visible
            if (selectedIndex >= 0 && selectedIndex < gamePanel.Controls.Count)
            {
                gamePanel.Controls[selectedIndex].Focus();
                ScrollToControl(gamePanel.Controls[selectedIndex], 0);
            }

            // Force the TableLayoutPanel to refresh its layout
            gamePanel.PerformLayout();
            LogToFile($"Step 5 - TableLayoutPanel layout refreshed. RowCount: {gamePanel.RowCount}");
            for (int row = 0; row < gamePanel.RowCount; row++)
            {
                LogToFile($"Row {row} Height: {gamePanel.GetRowHeights()[row]}");
            }
        }

        // Create a composite image by combining the cover art with the perimeter shadow
        private Image CreateCompositeImage(Image coverImage, int boxWidth, int boxHeight)
        {
            // Calculate the scaled dimensions of the perimeter shadow
            int perimeterWidth = boxWidth + 20; // 10 pixels on each side
            int perimeterHeight = boxHeight + 20; // 10 pixels on each side

            // Create a new bitmap for the composite image
            Bitmap composite = new Bitmap(perimeterWidth, perimeterHeight);
            using (Graphics g = Graphics.FromImage(composite))
            {
                // Fill the background with the TableLayoutPanel's background color to avoid blending issues
                g.Clear(ColorTranslator.FromHtml("#555555")); // Match the container background

                // Draw the perimeter shadow
                g.DrawImage(perimeterShadow, 0, 0, perimeterWidth, perimeterHeight);

                // Draw the cover art
                int coverX = 10; // Offset by 10 pixels to account for the perimeter
                int coverY = 10;
                g.DrawImage(coverImage, coverX, coverY, boxWidth, boxHeight);

                // Draw the 3-pixel border as a darkening overlay (alpha 80)
                using (var path = new GraphicsPath())
                {
                    path.AddRectangle(new Rectangle(10, 10, boxWidth - 1, boxHeight - 1)); // Offset by 10 pixels for the perimeter
                    using (var brush = new SolidBrush(Color.FromArgb(80, 0, 0, 0))) // Semi-transparent black, alpha 80
                    {
                        using (var pen = new Pen(brush, 3)) // 3px wide
                        {
                            g.DrawPath(pen, path);
                        }
                    }
                }
            }

            // Log the composite image dimensions
            LogToFile($"Step 3 - Created composite image: Width={composite.Width}, Height={composite.Height}, ArtBox Width={boxWidth}, ArtBox Height={boxHeight}");

            return composite;
        }

        private void SetupMonitors()
        {
            var screens = Screen.AllScreens;
            if (screens.Length >= 3)
            {
                // Monitor #1: Main UI (already set)
                this.Location = screens[0].Bounds.Location;

                // Monitor #2: Marquee
                marqueeForm = new Form
                {
                    FormBorderStyle = FormBorderStyle.None,
                    WindowState = FormWindowState.Maximized,
                    Location = screens[1].Bounds.Location
                };
                var marqueePicture = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.StretchImage };
                if (File.Exists(Path.Combine(Program.InstallDir, "default_marquee.png")))
                {
                    marqueePicture.Image = Image.FromFile(Path.Combine(Program.InstallDir, "default_marquee.png"));
                }
                marqueeForm.Controls.Add(marqueePicture);
                marqueeForm.Show();

                // Monitor #3: Controller Layout
                controllerForm = new Form
                {
                    FormBorderStyle = FormBorderStyle.None,
                    WindowState = FormWindowState.Maximized,
                    Location = screens[2].Bounds.Location
                };
                var controllerPicture = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.StretchImage };
                if (File.Exists(Path.Combine(Program.InstallDir, "default_controller.png")))
                {
                    controllerPicture.Image = Image.FromFile(Path.Combine(Program.InstallDir, "default_controller.png"));
                }
                controllerForm.Controls.Add(controllerPicture);
                controllerForm.Show();
            }
        }

        private void SetupInputHandling()
        {
            this.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape) // Direct check for Escape key
                {
                    Application.Exit();
                }

                // Map the key codes to the expected strings in settings.InputMappings using a traditional switch statement
                string keyString;
                switch (e.KeyCode)
                {
                    case Keys.Left:
                        keyString = "LeftArrow";
                        break;
                    case Keys.Right:
                        keyString = "RightArrow";
                        break;
                    case Keys.Up:
                        keyString = "UpArrow";
                        break;
                    case Keys.Down:
                        keyString = "DownArrow";
                        break;
                    default:
                        keyString = e.KeyCode.ToString();
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
                    // Start the hold detection timer to distinguish between tap and hold
                    if (!isHoldingUp)
                    {
                        isHoldingUp = true;
                        continuousScrollDirection = -1; // Scroll up
                        holdDetectionTimer.Tag = "Up";
                        holdDetectionTimer.Start();
                    }
                    // Trigger a discrete scroll for the first press
                    MoveSelection(-settings.NumberOfColumns);
                }
                else if (settings.InputMappings["Down"].Contains(keyString, StringComparer.OrdinalIgnoreCase))
                {
                    // Start the hold detection timer to distinguish between tap and hold
                    if (!isHoldingDown)
                    {
                        isHoldingDown = true;
                        continuousScrollDirection = 1; // Scroll down
                        holdDetectionTimer.Tag = "Down";
                        holdDetectionTimer.Start();
                    }
                    // Trigger a discrete scroll for the first press
                    MoveSelection(settings.NumberOfColumns);
                }
                else if (settings.InputMappings["Select"].Contains(keyString, StringComparer.OrdinalIgnoreCase))
                {
                    LaunchGame(games[selectedIndex]);
                }
                else if (settings.InputMappings.ContainsKey("Exit") && settings.InputMappings["Exit"].Contains(keyString, StringComparer.OrdinalIgnoreCase))
                {
                    Application.Exit();
                }
            };

            this.KeyUp += (s, e) =>
            {
                string keyString;
                switch (e.KeyCode)
                {
                    case Keys.Up:
                        keyString = "UpArrow";
                        break;
                    case Keys.Down:
                        keyString = "DownArrow";
                        break;
                    default:
                        keyString = e.KeyCode.ToString();
                        break;
                }

                if (settings.InputMappings["Up"].Contains(keyString, StringComparer.OrdinalIgnoreCase))
                {
                    isHoldingUp = false;
                    if (!isHoldingDown) // Stop continuous scrolling only if both keys are released
                    {
                        continuousScrollDirection = 0;
                        continuousScrollTimer.Stop();
                        holdDetectionTimer.Stop();
                        if (isContinuousScrolling)
                        {
                            isContinuousScrolling = false;
                            // Snap to the nearest row only after continuous scrolling
                            SnapToNearestRow();
                        }
                        else
                        {
                        }
                    }
                }
                else if (settings.InputMappings["Down"].Contains(keyString, StringComparer.OrdinalIgnoreCase))
                {
                    isHoldingDown = false;
                    if (!isHoldingUp) // Stop continuous scrolling only if both keys are released
                    {
                        continuousScrollDirection = 0;
                        continuousScrollTimer.Stop();
                        holdDetectionTimer.Stop();
                        if (isContinuousScrolling)
                        {
                            isContinuousScrolling = false;
                            // Snap to the nearest row only after continuous scrolling
                            SnapToNearestRow();
                        }
                        else
                        {
                        }
                    }
                }
            };
        }

        private void HoldDetectionTimer_Tick(object sender, EventArgs e)
        {
            // If the key is still held after the delay, start continuous scrolling
            holdDetectionTimer.Stop();
            if ((isHoldingUp && continuousScrollDirection == -1) || (isHoldingDown && continuousScrollDirection == 1))
            {
                isContinuousScrolling = true;
                continuousScrollTimer.Start();
                LogToFile($"HoldDetectionTimer_Tick: Starting continuous scrolling, Direction={continuousScrollDirection}");
            }
        }

        private void MoveSelection(int delta)
        {
            int newIndex = selectedIndex + delta;
            int columns = settings.NumberOfColumns > 0 ? settings.NumberOfColumns : 7;
            int rows = (int)Math.Ceiling((double)games.Count / columns);

            // Handle the case where we're moving down from the second-to-last row
            if (delta > 0) // Moving down
            {
                int currentRow = selectedIndex / columns;
                int currentCol = selectedIndex % columns;
                int targetRow = (selectedIndex + delta) / columns;

                // If we're on the second-to-last row and moving to the last row
                if (currentRow == rows - 2 && targetRow == rows - 1)
                {
                    int lastRowArtBoxes = games.Count % columns == 0 ? columns : games.Count % columns;
                    if (currentCol >= lastRowArtBoxes)
                    {
                        // Move to the last art box in the last row
                        newIndex = games.Count - 1;
                    }
                }
            }

            if (newIndex >= 0 && newIndex < games.Count)
            {
                // Update the highlight by invalidating the PictureBox controls
                if (selectedIndex >= 0 && selectedIndex < gamePanel.Controls.Count)
                {
                    gamePanel.Controls[selectedIndex].Invalidate();
                }
                selectedIndex = newIndex;
                if (selectedIndex >= 0 && selectedIndex < gamePanel.Controls.Count)
                {
                    gamePanel.Controls[selectedIndex].Invalidate();
                    gamePanel.Controls[selectedIndex].Focus();
                    // Start a smooth scroll animation to the selected art box
                    StartDiscreteScroll(gamePanel.Controls[selectedIndex], delta);
                    LogToFile($"Moved to PictureBox {selectedIndex}: Row={selectedIndex / columns}, Col={selectedIndex % columns}");
                }
            }
        }

        private void StartDiscreteScroll(Control control, int delta)
        {
            // Stop any ongoing discrete scroll animation
            discreteScrollTimer.Stop();
            isScrolling = false;

            // Calculate the target Y position (same logic as ScrollToControl)
            int rowTop = control.Location.Y;
            int rowBottom = control.Location.Y + control.Height;
            int containerHeight = gamePanelContainer.ClientSize.Height;
            currentScrollY = -gamePanel.Location.Y; // Current scroll position (inverted due to how Location works)

            // Check if the target row is already fully visible
            if (rowTop >= currentScrollY && rowBottom <= currentScrollY + containerHeight)
            {
                LogToFile($"StartDiscreteScroll: Row already fully visible, no scrolling needed. RowTop={rowTop}, RowBottom={rowBottom}, CurrentY={currentScrollY}, ContainerHeight={containerHeight}");
                return;
            }

            int topMargin = (int)gamePanel.RowStyles[0].Height; // Top margin (same as bottom margin)

            if (delta > 0) // Scrolling down
            {
                // Scroll down just enough to make the target row fully visible, plus a bottom margin
                targetScrollY = rowBottom + topMargin - containerHeight;
                if (targetScrollY < 0) targetScrollY = 0; // Don't scroll above the top
            }
            else if (delta < 0) // Scrolling up
            {
                // Scroll up just enough to make the target row fully visible, plus a top margin
                targetScrollY = rowTop - topMargin;
                if (targetScrollY < 0) targetScrollY = 0; // Don't scroll above the top
            }
            else // Initial load or no direction (e.g., first focus)
            {
                // Center the control in the view (used for initial load)
                targetScrollY = rowTop - (containerHeight / 2 - control.Height / 2);
                if (targetScrollY < 0) targetScrollY = 0; // Don't scroll above the top
            }

            // Ensure the scroll position doesn't exceed the maximum
            int maxScrollY = gamePanel.Height - containerHeight;
            if (maxScrollY < 0) maxScrollY = 0; // If the panel is smaller than the container
            if (targetScrollY > maxScrollY) targetScrollY = maxScrollY;

            // Calculate the scroll step (pixels per tick)
            int totalDistance = Math.Abs(targetScrollY - currentScrollY);
            int totalTicks = DiscreteAnimationDurationMs / DiscreteTimerIntervalMs;
            if (totalTicks > 0)
            {
                scrollStep = totalDistance / totalTicks;
                if (scrollStep == 0) scrollStep = 1; // Ensure at least 1 pixel per tick
                if (targetScrollY < currentScrollY) scrollStep = -scrollStep; // Adjust direction
            }
            else
            {
                scrollStep = totalDistance; // If ticks are 0, move the entire distance in one step
                if (targetScrollY < currentScrollY) scrollStep = -scrollStep;
            }

            // Start the discrete scroll animation
            isScrolling = true;
            discreteScrollTimer.Start();
            LogToFile($"StartDiscreteScroll: CurrentY={currentScrollY}, TargetY={targetScrollY}, ScrollStep={scrollStep}, TotalTicks={totalTicks}");
        }

        private void DiscreteScrollTimer_Tick(object sender, EventArgs e)
        {
            if (!isScrolling) return;

            // Update the current scroll position
            currentScrollY += scrollStep;

            // Check if we've reached or overshot the target
            if ((scrollStep > 0 && currentScrollY >= targetScrollY) || (scrollStep < 0 && currentScrollY <= targetScrollY))
            {
                currentScrollY = targetScrollY; // Snap to the target position
                discreteScrollTimer.Stop();
                isScrolling = false;
            }

            // Update the panel's position
            gamePanel.Location = new Point(gamePanel.Location.X, -currentScrollY);
            LogToFile($"DiscreteScrollTimer_Tick: CurrentY={currentScrollY}, TargetY={targetScrollY}");
        }

        private void ContinuousScrollTimer_Tick(object sender, EventArgs e)
        {
            if (continuousScrollDirection == 0 || !isContinuousScrolling) return;

            // Calculate the scroll distance per tick (pixels per second * seconds per tick)
            float secondsPerTick = ContinuousTimerIntervalMs / 1000f;
            int scrollDistance = (int)(ContinuousScrollSpeed * secondsPerTick * continuousScrollDirection);

            // Update the current scroll position
            currentScrollY = -gamePanel.Location.Y; // Current scroll position
            currentScrollY += scrollDistance;

            // Ensure the scroll position stays within bounds
            int containerHeight = gamePanelContainer.ClientSize.Height;
            int maxScrollY = gamePanel.Height - containerHeight;
            if (maxScrollY < 0) maxScrollY = 0; // If the panel is smaller than the container
            if (currentScrollY < 0) currentScrollY = 0;
            if (currentScrollY > maxScrollY) currentScrollY = maxScrollY;

            // Update the panel's position
            gamePanel.Location = new Point(gamePanel.Location.X, -currentScrollY);

            // Update the selection based on the current scroll position
            UpdateSelectionDuringContinuousScroll();

            LogToFile($"ContinuousScrollTimer_Tick: CurrentY={currentScrollY}, Direction={continuousScrollDirection}");
        }

        private void UpdateSelectionDuringContinuousScroll()
        {
            if (!isContinuousScrolling) return; // Only update during continuous scrolling

            int columns = settings.NumberOfColumns > 0 ? settings.NumberOfColumns : 7;
            int currentCol = selectedIndex % columns;
            int containerHeight = gamePanelContainer.ClientSize.Height;
            int currentY = -gamePanel.Location.Y; // Current scroll position
            int rows = (int)Math.Ceiling((double)games.Count / columns);

            // Check if we're at the top or bottom of the scroll range
            int maxScrollY = gamePanel.Height - containerHeight;
            if (maxScrollY < 0) maxScrollY = 0;

            if (currentY <= 0)
            {
                // At the top, select the first row
                int newIndex = currentCol; // First row, same column
                if (newIndex >= 0 && newIndex < games.Count && selectedIndex != newIndex)
                {
                    if (selectedIndex >= 0 && selectedIndex < gamePanel.Controls.Count)
                    {
                        gamePanel.Controls[selectedIndex].Invalidate();
                    }
                    selectedIndex = newIndex;
                    if (selectedIndex >= 0 && selectedIndex < gamePanel.Controls.Count)
                    {
                        gamePanel.Controls[selectedIndex].Invalidate();
                        gamePanel.Controls[selectedIndex].Focus();
                        LogToFile($"UpdateSelectionDuringContinuousScroll: At top, NewIndex={selectedIndex}, Row=0, Col={currentCol}");
                    }
                }
                return;
            }
            else if (currentY >= maxScrollY)
            {
                // At the bottom, select the last row
                int lastRow = rows - 1;
                int newIndex = lastRow * columns + currentCol;
                if (newIndex >= games.Count) // Adjust for partial last row
                {
                    newIndex = games.Count - 1; // Last art box
                }
                if (newIndex >= 0 && newIndex < games.Count && selectedIndex != newIndex)
                {
                    if (selectedIndex >= 0 && selectedIndex < gamePanel.Controls.Count)
                    {
                        gamePanel.Controls[selectedIndex].Invalidate();
                    }
                    selectedIndex = newIndex;
                    if (selectedIndex >= 0 && selectedIndex < gamePanel.Controls.Count)
                    {
                        gamePanel.Controls[selectedIndex].Invalidate();
                        gamePanel.Controls[selectedIndex].Focus();
                        LogToFile($"UpdateSelectionDuringContinuousScroll: At bottom, NewIndex={selectedIndex}, Row={lastRow}, Col={currentCol}");
                    }
                }
                return;
            }

            // Otherwise, find the row that is currently centered in the view
            int centerY = currentY + (containerHeight / 2);
            int newRow = -1;

            // Iterate through the rows to find the one that contains the centerY position
            for (int row = 0; row < gamePanel.RowCount; row++)
            {
                int rowTop = 0;
                for (int r = 0; r < row; r++)
                {
                    rowTop += (int)gamePanel.RowStyles[r].Height;
                }
                int rowBottom = rowTop + (int)gamePanel.RowStyles[row].Height;

                if (centerY >= rowTop && centerY <= rowBottom)
                {
                    // Adjust for the top margin row (row 0)
                    if (row % 2 == 0) continue; // Skip gap rows and top margin
                    newRow = (row - 1) / 2; // Convert TableLayoutPanel row index to game row index
                    break;
                }
            }

            if (newRow >= 0)
            {
                int newIndex = newRow * columns + currentCol;
                if (newIndex >= 0 && newIndex < games.Count)
                {
                    if (selectedIndex != newIndex)
                    {
                        // Update the highlight by invalidating the PictureBox controls
                        if (selectedIndex >= 0 && selectedIndex < gamePanel.Controls.Count)
                        {
                            gamePanel.Controls[selectedIndex].Invalidate();
                        }
                        selectedIndex = newIndex;
                        if (selectedIndex >= 0 && selectedIndex < gamePanel.Controls.Count)
                        {
                            gamePanel.Controls[selectedIndex].Invalidate();
                            gamePanel.Controls[selectedIndex].Focus();
                            LogToFile($"UpdateSelectionDuringContinuousScroll: NewIndex={selectedIndex}, Row={newRow}, Col={currentCol}");
                        }
                    }
                }
            }
        }

        private void SnapToNearestRow()
        {
            if (!isContinuousScrolling) return; // Only snap after continuous scrolling

            int columns = settings.NumberOfColumns > 0 ? settings.NumberOfColumns : 7;
            int currentCol = selectedIndex % columns;
            int containerHeight = gamePanelContainer.ClientSize.Height;
            int currentY = -gamePanel.Location.Y; // Current scroll position
            int rows = (int)Math.Ceiling((double)games.Count / columns);

            // Check if we're at the top or bottom of the scroll range
            int maxScrollY = gamePanel.Height - containerHeight;
            if (maxScrollY < 0) maxScrollY = 0;

            if (currentY <= 0)
            {
                // At the top, select the first row
                int newIndex = currentCol; // First row, same column
                if (newIndex >= 0 && newIndex < games.Count)
                {
                    if (selectedIndex != newIndex)
                    {
                        if (selectedIndex >= 0 && selectedIndex < gamePanel.Controls.Count)
                        {
                            gamePanel.Controls[selectedIndex].Invalidate();
                        }
                        selectedIndex = newIndex;
                        if (selectedIndex >= 0 && selectedIndex < gamePanel.Controls.Count)
                        {
                            gamePanel.Controls[selectedIndex].Invalidate();
                            gamePanel.Controls[selectedIndex].Focus();
                            StartDiscreteScroll(gamePanel.Controls[selectedIndex], continuousScrollDirection);
                            LogToFile($"SnapToNearestRow: At top, NewIndex={selectedIndex}, Row=0, Col={currentCol}");
                        }
                    }
                }
                return;
            }
            else if (currentY >= maxScrollY)
            {
                // At the bottom, select the last row
                int lastRow = rows - 1;
                int newIndex = lastRow * columns + currentCol;
                if (newIndex >= games.Count) // Adjust for partial last row
                {
                    newIndex = games.Count - 1; // Last art box
                }
                if (newIndex >= 0 && newIndex < games.Count)
                {
                    if (selectedIndex != newIndex)
                    {
                        if (selectedIndex >= 0 && selectedIndex < gamePanel.Controls.Count)
                        {
                            gamePanel.Controls[selectedIndex].Invalidate();
                        }
                        selectedIndex = newIndex;
                        if (selectedIndex >= 0 && selectedIndex < gamePanel.Controls.Count)
                        {
                            gamePanel.Controls[selectedIndex].Invalidate();
                            gamePanel.Controls[selectedIndex].Focus();
                            StartDiscreteScroll(gamePanel.Controls[selectedIndex], continuousScrollDirection);
                            LogToFile($"SnapToNearestRow: At bottom, NewIndex={selectedIndex}, Row={lastRow}, Col={currentCol}");
                        }
                    }
                }
                return;
            }

            // Otherwise, find the row that is currently centered in the view
            int centerY = currentY + (containerHeight / 2);
            int nearestRow = -1;

            // Iterate through the rows to find the one that contains the centerY position
            for (int row = 0; row < gamePanel.RowCount; row++)
            {
                int rowTop = 0;
                for (int r = 0; r < row; r++)
                {
                    rowTop += (int)gamePanel.RowStyles[r].Height;
                }
                int rowBottom = rowTop + (int)gamePanel.RowStyles[row].Height;

                if (centerY >= rowTop && centerY <= rowBottom)
                {
                    // Adjust for the top margin row (row 0)
                    if (row % 2 == 0) continue; // Skip gap rows and top margin
                    nearestRow = (row - 1) / 2; // Convert TableLayoutPanel row index to game row index
                    break;
                }
            }

            if (nearestRow >= 0)
            {
                int newIndex = nearestRow * columns + currentCol;
                if (newIndex >= 0 && newIndex < games.Count)
                {
                    if (selectedIndex != newIndex)
                    {
                        // Update the highlight by invalidating the PictureBox controls
                        if (selectedIndex >= 0 && selectedIndex < gamePanel.Controls.Count)
                        {
                            gamePanel.Controls[selectedIndex].Invalidate();
                        }
                        selectedIndex = newIndex;
                        if (selectedIndex >= 0 && selectedIndex < gamePanel.Controls.Count)
                        {
                            gamePanel.Controls[selectedIndex].Invalidate();
                            gamePanel.Controls[selectedIndex].Focus();
                            // Start a smooth scroll animation to the nearest row
                            StartDiscreteScroll(gamePanel.Controls[selectedIndex], continuousScrollDirection);
                            LogToFile($"SnapToNearestRow: NewIndex={selectedIndex}, Row={nearestRow}, Col={currentCol}");
                        }
                    }
                    else
                    {
                        // If the selection didn't change, still scroll to the nearest row
                        StartDiscreteScroll(gamePanel.Controls[selectedIndex], continuousScrollDirection);
                    }
                }
            }
        }

        private void ScrollToControl(Control control, int delta)
        {
            // Calculate the target row's top and bottom positions
            int rowTop = control.Location.Y;
            int rowBottom = control.Location.Y + control.Height;
            int containerHeight = gamePanelContainer.ClientSize.Height;
            currentScrollY = -gamePanel.Location.Y; // Current scroll position (inverted due to how Location works)

            // Check if the target row is already fully visible
            if (rowTop >= currentScrollY && rowBottom <= currentScrollY + containerHeight)
            {
                LogToFile($"ScrollToControl: Row already fully visible, no scrolling needed. RowTop={rowTop}, RowBottom={rowBottom}, CurrentY={currentScrollY}, ContainerHeight={containerHeight}");
                return;
            }

            int targetY;
            int topMargin = (int)gamePanel.RowStyles[0].Height; // Top margin (same as bottom margin)

            if (delta > 0) // Scrolling down
            {
                // Scroll down just enough to make the target row fully visible, plus a bottom margin
                targetY = rowBottom + topMargin - containerHeight;
                if (targetY < 0) targetY = 0; // Don't scroll above the top
            }
            else if (delta < 0) // Scrolling up
            {
                // Scroll up just enough to make the target row fully visible, plus a top margin
                targetY = rowTop - topMargin;
                if (targetY < 0) targetY = 0; // Don't scroll above the top
            }
            else // Initial load or no direction (e.g., first focus)
            {
                // Center the control in the view (used for initial load)
                targetY = rowTop - (containerHeight / 2 - control.Height / 2);
                if (targetY < 0) targetY = 0; // Don't scroll above the top
            }

            // Ensure the scroll position doesn't exceed the maximum
            int maxScrollY = gamePanel.Height - containerHeight;
            if (maxScrollY < 0) maxScrollY = 0; // If the panel is smaller than the container
            if (targetY > maxScrollY) targetY = maxScrollY;

            // Update the panel's position directly for initial load
            if (delta == 0)
            {
                gamePanel.Location = new Point(gamePanel.Location.X, -targetY);
                LogToFile($"ScrollToControl (Initial): TargetY={targetY}, CurrentY={currentScrollY}, ContainerHeight={containerHeight}, PanelHeight={gamePanel.Height}, Delta={delta}");
            }
        }

        private void LaunchGame(Game game)
        {
            try
            {
                // Update Monitor #2 and #3 (only if they exist)
                if (marqueeForm != null && marqueeForm.Controls.Count > 0 && marqueeForm.Controls[0] is PictureBox marqueePicture && File.Exists(game.MarqueePath))
                {
                    marqueePicture.Image = Image.FromFile(game.MarqueePath);
                }
                if (controllerForm != null && controllerForm.Controls.Count > 0 && controllerForm.Controls[0] is PictureBox controllerPicture && File.Exists(game.ControllerLayoutPath))
                {
                    controllerPicture.Image = Image.FromFile(game.ControllerLayoutPath);
                }

                // Run Pre-Launch Commands
                foreach (var cmd in game.PreLaunchCommands ?? new List<string>())
                {
                    System.Diagnostics.Process.Start("cmd.exe", $"/c {cmd}");
                }

                // Run LEDBlinky Animation
                if (!string.IsNullOrEmpty(game.LEDBlinkyCommand))
                {
                    System.Diagnostics.Process.Start("cmd.exe", $"/c {game.LEDBlinkyCommand}");
                }

                // Launch Game
                if (game.Type == "PC")
                {
                    System.Diagnostics.Process.Start(game.ExecutablePath);
                }
                else if (game.Type == "Emulated")
                {
                    var plugin = plugins.FirstOrDefault(p => p.Name == game.EmulatorPlugin);
                    if (plugin != null)
                    {
                        plugin.PreLaunch(game.EmulatorPath, game.RomPath);
                        var cmd = plugin.BuildLaunchCommand(game.EmulatorPath, game.RomPath, game.CustomParameters);
                        System.Diagnostics.Process.Start("cmd.exe", $"/c {cmd}").WaitForExit();
                        plugin.PostExit(game.EmulatorPath, game.RomPath);
                    }
                }

                // Run Post-Exit Commands
                foreach (var cmd in game.PostExitCommands ?? new List<string>())
                {
                    System.Diagnostics.Process.Start("cmd.exe", $"/c {cmd}");
                }

                // Revert Monitor #2 and #3 (only if they exist)
                if (marqueeForm != null && marqueeForm.Controls.Count > 0 && marqueeForm.Controls[0] is PictureBox marqueePicture2 && File.Exists(Path.Combine(Program.InstallDir, "default_marquee.png")))
                {
                    marqueePicture2.Image = Image.FromFile(Path.Combine(Program.InstallDir, "default_marquee.png"));
                }
                if (controllerForm != null && controllerForm.Controls.Count > 0 && controllerForm.Controls[0] is PictureBox controllerPicture2 && File.Exists(Path.Combine(Program.InstallDir, "default_controller.png")))
                {
                    controllerPicture2.Image = Image.FromFile(Path.Combine(Program.InstallDir, "default_controller.png"));
                }
            }
            catch (Exception ex)
            {
                var messageBox = new Form
                {
                    Text = "Error",
                    Size = new Size(400, 200),
                    StartPosition = FormStartPosition.CenterScreen
                };
                var label = new Label
                {
                    Text = $"Application failed to Launch\n\nDetails: {ex.Message}",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter
                };
                messageBox.Controls.Add(label);
                messageBox.KeyDown += (s, e) =>
                {
                    if (settings.InputMappings["Select"].Contains(e.KeyCode.ToString(), StringComparer.OrdinalIgnoreCase))
                    {
                        messageBox.Close();
                    }
                };
                messageBox.ShowDialog();
            }
        }
    }
}