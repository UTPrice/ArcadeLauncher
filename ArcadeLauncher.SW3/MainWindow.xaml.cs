using System;
using System.Collections.Generic;
using System.ComponentModel; // For INotifyPropertyChanged
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Diagnostics;
using ArcadeLauncher.Core;
using ArcadeLauncher.Plugins;
using System.Threading.Tasks;

// Suppress CA1416 warnings for the entire assembly since this application is Windows-specific
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "This application is Windows-specific.")]

namespace ArcadeLauncher.SW3
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private List<Game> games = null!;
        private Settings settings = null!;
        private List<IEmulatorPlugin> plugins = null!;
        private int selectedIndex = 0;
        private ScrollViewer scrollViewer = null!;
        private ItemsControl gameItemsControl = null!;
        private Window marqueeWindow = null!;
        private Window controllerWindow = null!;
        private float dpiScaleFactor; // DPI scaling factor (e.g., 1.25 for 125% scaling)
        private List<Bitmap> preloadedImages = null!; // Store preloaded composite images
        private double topMargin; // Store the top margin for use in scrolling methods
        private double adjustedVerticalGap; // Store the vertical gap between rows (calculated overlap)
        public double borderThickness; // Store the border thickness for scrolling calculations (made public for binding)
        private double actualVerticalGap; // Store the actual gap between rows as logged
        private double totalCalculatedWidth; // Store the total calculated width for use in SetupComponents
        private Canvas canvas = null!; // Use a Canvas to prevent stretching
        private Border marginBorder = null!; // Border to apply left and right margins
        private int rows;

        // INotifyPropertyChanged implementation
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Property for SelectedIndex to enable binding
        public int SelectedIndex
        {
            get => selectedIndex;
            set
            {
                if (selectedIndex != value)
                {
                    selectedIndex = value;
                    OnPropertyChanged(nameof(SelectedIndex));
                }
            }
        }

        public MainWindow()
        {
            // Set the DataContext early to ensure bindings work
            this.DataContext = this;

            // Set the window size to the screen's logical dimensions
            this.Width = SystemParameters.PrimaryScreenWidth;
            this.Height = SystemParameters.PrimaryScreenHeight;
            this.WindowStyle = WindowStyle.None; // Full-screen, no borders
            this.WindowState = WindowState.Maximized; // Ensure full-screen
            this.ResizeMode = ResizeMode.NoResize; // Prevent resizing
            this.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(85, 85, 85)); // Restore original background color #555555

            InitializeComponent();
            SetupComponents();
            LoadData();

            // Initialize smooth scrolling (minimal implementation)
            InitializeScrolling();

            // Initialize game logic
            InitializeGameLogic();

            Loaded += (s, e) =>
            {
                // Calculate the DPI scaling factor after the window is loaded
                var presentationSource = PresentationSource.FromVisual(this);
                if (presentationSource != null)
                {
                    dpiScaleFactor = (float)(presentationSource.CompositionTarget.TransformToDevice.M11);
                    LogToFile($"DPI Scaling Factor: {dpiScaleFactor}");
                }
                else
                {
                    // Fallback method using System.Drawing.Graphics
                    try
                    {
                        using (var bitmap = new Bitmap(1, 1))
                        using (var g = Graphics.FromImage(bitmap))
                        {
                            dpiScaleFactor = g.DpiX / 96f;
                            LogToFile($"DPI Scaling Factor (Fallback): {dpiScaleFactor} (DPI: {g.DpiX})");
                        }
                    }
                    catch (Exception ex)
                    {
                        dpiScaleFactor = 1.0f;
                        LogToFile($"Failed to retrieve DPI using fallback method: {ex.Message}, defaulting dpiScaleFactor to 1.0");
                    }
                }

                SetupUI();

                // Move the mouse cursor to the bottom-right pixel of the primary screen
                var screenWidth = SystemParameters.PrimaryScreenWidth;
                var screenHeight = SystemParameters.PrimaryScreenHeight;
                Mouse.OverrideCursor = System.Windows.Input.Cursors.None; // Hide cursor
                System.Windows.Forms.Cursor.Position = new System.Drawing.Point((int)screenWidth - 1, (int)screenHeight - 1);
                LogToFile($"Moved cursor to bottom-right pixel: ({screenWidth - 1}, {screenHeight - 1})");

                // Setup monitors after ensuring the main window is initialized
                SetupMonitors();

                // Ensure the selected game is visible after the layout pass
                if (SelectedIndex >= 0 && SelectedIndex < games.Count)
                {
                    var selectedItem = gameItemsControl.ItemContainerGenerator.ContainerFromIndex(SelectedIndex) as FrameworkElement;
                    if (selectedItem != null)
                    {
                        selectedItem.Focus(); // Set initial focus
                        ScrollToSelectedItem(); // Use ScrollToSelectedItem
                        LogToFile($"Initial focus set to item at index {SelectedIndex}");
                    }
                    else
                    {
                        LogToFile($"Selected item at index {SelectedIndex} is null after layout");
                    }
                }
                else
                {
                    LogToFile($"SelectedIndex {SelectedIndex} is out of range (games.Count: {games.Count})");
                }

                // Ensure the main window has focus
                this.Activate();
                this.Focus();
                LogToFile("Main window activated and focused.");

                // Schedule diagnostic logging in a background thread to avoid blocking the UI thread
                Task.Run(() => PerformDiagnosticLogging());
            };
        }

        private async Task PerformDiagnosticLogging()
        {
            try
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    // Log the Window's size
                    LogToFile($"Window Size - Width: {this.ActualWidth}, Height: {this.ActualHeight}");

                    // Log the screen's logical dimensions for reference
                    double screenLogicalWidth = SystemParameters.PrimaryScreenWidth;
                    double screenLogicalHeight = SystemParameters.PrimaryScreenHeight;
                    LogToFile($"Screen Logical Dimensions - Width: {screenLogicalWidth}, Height: {screenLogicalHeight}");

                    // Log the physical dimensions (logical * DPI scaling)
                    double screenPhysicalWidth = screenLogicalWidth * dpiScaleFactor;
                    double screenPhysicalHeight = screenLogicalHeight * dpiScaleFactor;
                    LogToFile($"Screen Physical Dimensions (Logical * DPI Scaling) - Width: {screenPhysicalWidth}, Height: {screenPhysicalHeight}");

                    // Log the Canvas's position and size
                    var canvasPosition = canvas.TransformToAncestor(this).Transform(new System.Windows.Point(0, 0));
                    LogToFile($"Canvas Position - X: {canvasPosition.X}, Y: {canvasPosition.Y}, Width: {canvas.ActualWidth}, Height: {canvas.ActualHeight}");

                    // Log the MarginBorder's computed position and size
                    if (marginBorder != null)
                    {
                        var marginBorderPosition = marginBorder.TransformToAncestor(this).Transform(new System.Windows.Point(0, 0));
                        LogToFile($"MarginBorder Position - X: {marginBorderPosition.X}, Y: {marginBorderPosition.Y}, Width: {marginBorder.ActualWidth}, Height: {marginBorder.ActualHeight}");
                    }
                    else
                    {
                        LogToFile("MarginBorder is null");
                    }

                    // Log the ScrollViewer's computed size and position
                    var scrollViewerPosition = scrollViewer.TransformToAncestor(this).Transform(new System.Windows.Point(0, 0));
                    LogToFile($"ScrollViewer Position (relative to Window) - X: {scrollViewerPosition.X}, Y: {scrollViewerPosition.Y} (logical pixels), Width: {scrollViewer.ActualWidth}, Height: {scrollViewer.ActualHeight} (logical pixels)");

                    // Log the ItemsControl's padding
                    if (gameItemsControl != null)
                    {
                        LogToFile($"ItemsControl Padding - Left: {gameItemsControl.Padding.Left}, Right: {gameItemsControl.Padding.Right}, Top: {gameItemsControl.Padding.Top}, Bottom: {gameItemsControl.Padding.Bottom}");
                    }
                    else
                    {
                        LogToFile("gameItemsControl is null");
                    }

                    // Log the actual widths after layout
                    LogToFile($"After Layout - Canvas.ActualWidth: {canvas.ActualWidth}, MarginBorder.ActualWidth: {marginBorder?.ActualWidth ?? 0}, ScrollViewer.ActualWidth: {scrollViewer.ActualWidth}, ItemsControl.ActualWidth: {gameItemsControl?.ActualWidth ?? 0}");

                    // Log the positions of the first row's art boxes to verify gaps
                    if (gameItemsControl != null && gameItemsControl.Items.Count >= settings.NumberOfColumns)
                    {
                        for (int i = 0; i < settings.NumberOfColumns; i++) // First row
                        {
                            var item = gameItemsControl.ItemContainerGenerator.ContainerFromIndex(i) as FrameworkElement;
                            if (item != null)
                            {
                                var transform = item.TransformToAncestor(this);
                                var position = transform.Transform(new System.Windows.Point(0, 0));
                                LogToFile($"First Row ArtBox {i} Position (relative to Window) - X: {position.X}, Y: {position.Y}, Width: {item.ActualWidth}, Height: {item.ActualHeight}");
                                LogToFile($"First Row ArtBox {i} Margin - Left: {item.Margin.Left}, Right: {item.Margin.Right}, Top: {item.Margin.Top}, Bottom: {item.Margin.Bottom}");
                                // Calculate horizontal gap to the next art box
                                if (i < settings.NumberOfColumns - 1)
                                {
                                    var nextItem = gameItemsControl.ItemContainerGenerator.ContainerFromIndex(i + 1) as FrameworkElement;
                                    if (nextItem != null)
                                    {
                                        var nextTransform = nextItem.TransformToAncestor(this);
                                        var nextPosition = nextTransform.Transform(new System.Windows.Point(0, 0));
                                        double gap = nextPosition.X - (position.X + item.ActualWidth);
                                        LogToFile($"Horizontal Gap between ArtBox {i} and ArtBox {i + 1}: {gap}");
                                    }
                                }
                            }
                            else
                            {
                                LogToFile($"First Row ArtBox {i} is null");
                            }
                        }
                    }
                    else
                    {
                        LogToFile($"ItemsControl has insufficient items for first row logging: Count={gameItemsControl?.Items.Count ?? 0}");
                    }

                    // Log the positions of the first and last art boxes to calculate effective margins
                    if (gameItemsControl != null && gameItemsControl.Items.Count > 0)
                    {
                        var firstItem = gameItemsControl.ItemContainerGenerator.ContainerFromIndex(0) as FrameworkElement;
                        if (firstItem != null)
                        {
                            var transform = firstItem.TransformToAncestor(this);
                            var position = transform.Transform(new System.Windows.Point(0, 0));
                            LogToFile($"First ArtBox Position (relative to Window) - X: {position.X}, Y: {position.Y}, Width: {firstItem.ActualWidth}, Height: {firstItem.ActualHeight}");
                            // Calculate effective left margin
                            LogToFile($"Effective Left Margin (relative to Window): {position.X}");
                        }

                        // Find the rightmost art box in the first full row (not the last row, which may have fewer items)
                        int rightmostIndex = settings.NumberOfColumns - 1; // Last item in the first row
                        var rightmostItem = gameItemsControl.ItemContainerGenerator.ContainerFromIndex(rightmostIndex) as FrameworkElement;
                        if (rightmostItem != null)
                        {
                            var transform = rightmostItem.TransformToAncestor(this);
                            var position = transform.Transform(new System.Windows.Point(0, 0));
                            LogToFile($"Rightmost ArtBox in First Full Row (Index {rightmostIndex}) Position (relative to Window) - X: {position.X}, Y: {position.Y}, Width: {rightmostItem.ActualWidth}, Height: {rightmostItem.ActualHeight}");
                            // Calculate effective right margin
                            double rightEdge = position.X + rightmostItem.ActualWidth;
                            double effectiveRightMargin = this.ActualWidth - rightEdge;
                            LogToFile($"Effective Right Margin (relative to Window, based on first full row): {effectiveRightMargin}");
                        }

                        var lastItemIndex = gameItemsControl.Items.Count - 1;
                        var lastItem = gameItemsControl.ItemContainerGenerator.ContainerFromIndex(lastItemIndex) as FrameworkElement;
                        if (lastItem != null)
                        {
                            var transform = lastItem.TransformToAncestor(this);
                            var position = transform.Transform(new System.Windows.Point(0, 0));
                            LogToFile($"Last ArtBox Position (relative to Window) - X: {position.X}, Y: {position.Y}, Width: {lastItem.ActualWidth}, Height: {lastItem.ActualHeight}");
                        }
                    }

                    // Log the positions of ScrollViewer, ItemsPresenter, and UniformGrid
                    scrollViewerPosition = scrollViewer.TransformToAncestor(this).Transform(new System.Windows.Point(0, 0));
                    LogToFile($"ScrollViewer Position (relative to Window) - X: {scrollViewerPosition.X}, Y: {scrollViewerPosition.Y} (logical pixels), Width: {scrollViewer.ActualWidth}, Height: {scrollViewer.ActualHeight} (logical pixels)");

                    ItemsPresenter? itemsPresenter = null;
                    UniformGrid? uniformGrid = null;
                    FrameworkElement? firstBorder = null;

                    if (gameItemsControl != null)
                    {
                        itemsPresenter = FindVisualChild<ItemsPresenter>(gameItemsControl);
                        if (itemsPresenter != null)
                        {
                            uniformGrid = FindVisualChild<UniformGrid>(itemsPresenter);
                            if (uniformGrid != null)
                            {
                                LogToFile($"UniformGrid Details - Columns: {uniformGrid.Columns}, ActualWidth: {uniformGrid.ActualWidth}, ActualHeight: {uniformGrid.ActualHeight}");
                                // Calculate and log the effective row height with null check
                                double rowHeight = 0;
                                if (uniformGrid.Rows > 0) // Ensure Rows is not zero to avoid division by zero
                                {
                                    rowHeight = (uniformGrid.ActualHeight - gameItemsControl.Margin.Top) / uniformGrid.Rows;
                                }
                                else
                                {
                                    LogToFile("UniformGrid Rows is zero, cannot calculate row height.");
                                }
                                LogToFile($"Calculated UniformGrid Row Height: {rowHeight}");
                            }
                            else
                            {
                                LogToFile("UniformGrid not found in ItemsPresenter");
                            }
                        }
                        else
                        {
                            LogToFile("ItemsPresenter not found in ItemsControl");
                        }
                    }

                    if (itemsPresenter != null)
                    {
                        var itemsPresenterPosition = itemsPresenter.TransformToAncestor(scrollViewer).Transform(new System.Windows.Point(0, 0));
                        LogToFile($"ItemsPresenter Position (relative to ScrollViewer) - X: {itemsPresenterPosition.X}, Y: {itemsPresenterPosition.Y} (logical pixels), Width: {itemsPresenter.ActualWidth}, Height: {itemsPresenter.ActualHeight} (logical pixels)");
                        if (uniformGrid != null)
                        {
                            var uniformGridPosition = uniformGrid.TransformToAncestor(itemsPresenter).Transform(new System.Windows.Point(0, 0));
                            LogToFile($"UniformGrid Position (relative to ItemsPresenter) - X: {uniformGridPosition.X}, Y: {uniformGridPosition.Y} (logical pixels), Width: {uniformGrid.ActualWidth}, Height: {uniformGrid.ActualHeight} (logical pixels)");
                            if (gameItemsControl.Items.Count > 0)
                            {
                                firstBorder = gameItemsControl.ItemContainerGenerator.ContainerFromIndex(0) as FrameworkElement;
                                if (firstBorder != null && uniformGrid != null)
                                {
                                    var borderPosition = firstBorder.TransformToAncestor(uniformGrid).Transform(new System.Windows.Point(0, 0));
                                    LogToFile($"First Border Position (relative to UniformGrid) - X: {borderPosition.X}, Y: {borderPosition.Y} (logical pixels), Width: {firstBorder.ActualWidth}, Height: {firstBorder.ActualHeight} (logical pixels)");
                                }
                                else
                                {
                                    LogToFile("First Border or UniformGrid is null.");
                                }
                            }
                            else
                            {
                                LogToFile("ItemsControl has no items.");
                            }
                        }
                        else
                        {
                            LogToFile("UniformGrid is null.");
                        }
                    }
                    else
                    {
                        LogToFile("ItemsPresenter is null.");
                    }
                });
            }
            catch (Exception ex)
            {
                LogToFile($"Error in PerformDiagnosticLogging: {ex.Message}");
            }
        }

        // Helper method to find a visual child of a specific type
        private T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;

            // The null check above ensures parent is not null in the loop
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent!); i++)
            {
                var child = VisualTreeHelper.GetChild(parent!, i);
                if (child is T result)
                {
                    return result;
                }

                var descendant = FindVisualChild<T>(child);
                if (descendant != null)
                {
                    return descendant;
                }
            }
            return null;
        }

        private void SetupComponents()
        {
            // Create the main layout using a Canvas to prevent stretching
            canvas = new Canvas();
            canvas.Width = SystemParameters.PrimaryScreenWidth;
            canvas.Height = SystemParameters.PrimaryScreenHeight;
            Content = canvas;

            // Create a Border to apply left and right margins
            marginBorder = new Border
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(85, 85, 85)), // #555555
                Width = SystemParameters.PrimaryScreenWidth,
                Height = SystemParameters.PrimaryScreenHeight
            };
            Canvas.SetLeft(marginBorder, 0);
            Canvas.SetTop(marginBorder, 0);
            canvas.Children.Add(marginBorder);

            // Create a ScrollViewer for the game panel
            scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(85, 85, 85)), // #555555
                CanContentScroll = false, // Keep false to avoid logical scrolling issues
                Width = SystemParameters.PrimaryScreenWidth,
                Height = SystemParameters.PrimaryScreenHeight,
                Margin = new Thickness(0) // Remove the upward offset
            };
            Canvas.SetLeft(scrollViewer, 0);
            Canvas.SetTop(scrollViewer, 0); // Explicitly set Y position to 0
            // Enable double-buffering for the ScrollViewer
            scrollViewer.SetValue(RenderOptions.BitmapScalingModeProperty, BitmapScalingMode.HighQuality);
            // Optimize rendering
            scrollViewer.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);
            marginBorder.Child = scrollViewer;

            // Create an ItemsControl for the game art boxes
            gameItemsControl = new ItemsControl
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(85, 85, 85)), // #555555
                Padding = new Thickness(0) // Remove any default padding
            };
            // Enable double-buffering for the ItemsControl
            gameItemsControl.SetValue(RenderOptions.BitmapScalingModeProperty, BitmapScalingMode.HighQuality);
            scrollViewer.Content = gameItemsControl;

            // Log the initial sizes set in SetupComponents
            LogToFile($"SetupComponents: MainWindow Size - Width: {this.Width}, Height: {this.Height}");
            LogToFile($"SetupComponents: Canvas Size - Width: {canvas.Width}, Height: {canvas.Height}");
            LogToFile($"SetupComponents: MarginBorder Size - Width: {marginBorder.Width}, Height: {marginBorder.Height}");
            LogToFile($"SetupComponents: ScrollViewer Size - Width: {scrollViewer.Width}, Height: {scrollViewer.Height}");
        }

        private void LoadData()
        {
            games = DataManager.LoadGameData().Games
                .Where(g => !g.IsInProgress) // Filter out games where IsInProgress is true
                .OrderBy(g => g.AlphabetizeName)
                .ToList();
            settings = DataManager.LoadSettings();
            plugins = LoadPlugins();
        }

        private void LogToFile(string message)
        {
            try
            {
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string logDir = System.IO.Path.Combine(appDataPath, "ArcadeLauncher");
                Directory.CreateDirectory(logDir);
                string logFile = System.IO.Path.Combine(logDir, "SW3_Log.txt");
                File.AppendAllText(logFile, $"{DateTime.Now}: {message}\n");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to log: {ex.Message}");
            }
        }

        private Bitmap CreateCompositeImage(Bitmap coverImage, int boxWidth, int boxHeight)
        {
            // Create a new bitmap at the exact size of the art box
            Bitmap composite = new Bitmap(boxWidth, boxHeight);
            using (Graphics g = Graphics.FromImage(composite))
            {
                // Fill the background with the container background color
                g.Clear(System.Drawing.Color.FromArgb(85, 85, 85)); // #555555

                // Draw the cover art (scaled to fit exactly)
                g.DrawImage(coverImage, 0, 0, boxWidth, boxHeight);

                // Draw the 3-pixel border as a darkening overlay (alpha 80)
                using (var path = new System.Drawing.Drawing2D.GraphicsPath())
                {
                    path.AddRectangle(new RectangleF(0, 0, boxWidth - 1, boxHeight - 1));
                    using (var brush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(80, 0, 0, 0)))
                    {
                        using (var pen = new System.Drawing.Pen(brush, 3))
                        {
                            g.DrawPath(pen, path);
                        }
                    }
                }
            }

            LogToFile($"Step 3 - Created composite image: Width={composite.Width}, Height={composite.Height}, ArtBox Width={boxWidth}, ArtBox Height={boxHeight}");
            return composite;
        }

        private void SetImageSourceSafely(System.Windows.Controls.Image image, string imagePath)
        {
            try
            {
                // Resolve the absolute path
                string absolutePath = Path.GetFullPath(imagePath);
                LogToFile($"Attempting to load image for {image.Name}: {absolutePath}");

                if (File.Exists(absolutePath))
                {
                    image.Source = new BitmapImage(new Uri(absolutePath, UriKind.Absolute));
                    LogToFile($"Successfully loaded image for {image.Name}: {absolutePath}");
                }
                else
                {
                    image.Source = null;
                    LogToFile($"Image file not found, cleared source for {image.Name}: {absolutePath}");
                }
            }
            catch (Exception ex)
            {
                image.Source = null;
                LogToFile($"Failed to load image for {image.Name}: {imagePath}, Error: {ex.Message}");
            }
        }

        private void PositionWindowOnMonitor(Window window, int monitorIndex, string monitorRole)
        {
            var screens = System.Windows.Forms.Screen.AllScreens;
            if (monitorIndex >= 0 && monitorIndex < screens.Length)
            {
                var screen = screens[monitorIndex];
                // Convert physical pixels to logical pixels using the DPI scaling factor
                double logicalLeft = screen.Bounds.Left / dpiScaleFactor;
                double logicalTop = screen.Bounds.Top / dpiScaleFactor;
                double logicalWidth = screen.Bounds.Width / dpiScaleFactor;
                double logicalHeight = screen.Bounds.Height / dpiScaleFactor;

                window.Left = logicalLeft;
                window.Top = logicalTop;
                window.Width = logicalWidth;
                window.Height = logicalHeight;

                LogToFile($"{monitorRole} window positioned on Monitor {monitorIndex + 1} (Device: {screen.DeviceName}, Physical Bounds: {screen.Bounds}, Logical Bounds: Left={logicalLeft}, Top={logicalTop}, Width={logicalWidth}, Height={logicalHeight})");
            }
            else
            {
                // Fallback to primary monitor
                window.Left = screens[0].Bounds.Left / dpiScaleFactor;
                window.Top = screens[0].Bounds.Top / dpiScaleFactor;
                window.Width = screens[0].Bounds.Width / dpiScaleFactor;
                window.Height = screens[0].Bounds.Height / dpiScaleFactor;
                LogToFile($"Warning: {monitorRole} monitor index {monitorIndex} not found. Falling back to primary monitor (Device: {screens[0].DeviceName}, Physical Bounds: {screens[0].Bounds}, Logical Bounds: Left={window.Left}, Top={window.Top}, Width={window.Width}, Height={window.Height})");
            }
        }

        private void SetupMonitors()
        {
            LogToFile("Starting SetupMonitors...");
            try
            {
                // Ensure the main window is on Monitor 1 (primary monitor)
                PositionWindowOnMonitor(this, 0, "Main");

                var screens = System.Windows.Forms.Screen.AllScreens;
                LogToFile($"Detected {screens.Length} monitors:");
                for (int i = 0; i < screens.Length; i++)
                {
                    LogToFile($"Monitor {i + 1}: DeviceName={screens[i].DeviceName}, Primary={screens[i].Primary}, Bounds={screens[i].Bounds}");
                }

                if (screens.Length < 2)
                {
                    LogToFile("Warning: At least two monitors are required. Marquee and Controller windows will not be displayed.");
                    return;
                }

                // Log the InstallDir to debug image path
                LogToFile($"Program.InstallDir: {Program.InstallDir}");

                // Monitor #2: Marquee
                LogToFile("Creating marquee window...");
                marqueeWindow = new Window
                {
                    WindowStyle = WindowStyle.None,
                    ResizeMode = ResizeMode.NoResize,
                    ShowInTaskbar = false, // Hide from taskbar
                    Owner = this // Owned by the main window
                };
                var marqueeImage = new System.Windows.Controls.Image
                {
                    Stretch = Stretch.UniformToFill, // Stretch to fill the window, cropping if necessary
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    Name = "MarqueeImage"
                };
                marqueeWindow.Content = marqueeImage;
                string marqueeDefaultPath = System.IO.Path.Combine(Program.InstallDir, "default_marquee.png");
                SetImageSourceSafely(marqueeImage, marqueeDefaultPath);
                PositionWindowOnMonitor(marqueeWindow, 1, "Marquee");
                // Set the image size to match the window's logical dimensions
                marqueeImage.Width = marqueeWindow.Width;
                marqueeImage.Height = marqueeWindow.Height;
                marqueeWindow.Show();
                LogToFile("Marquee window shown on Monitor 2.");

                // Monitor #3: Controller Layout
                if (screens.Length >= 3)
                {
                    LogToFile("Creating controller window...");
                    controllerWindow = new Window
                    {
                        WindowStyle = WindowStyle.None,
                        ResizeMode = ResizeMode.NoResize,
                        ShowInTaskbar = false, // Hide from taskbar
                        Owner = this // Owned by the main window
                    };
                    var controllerImage = new System.Windows.Controls.Image
                    {
                        Stretch = Stretch.UniformToFill, // Stretch to fill the window, cropping if necessary
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Stretch,
                        Name = "ControllerImage"
                    };
                    controllerWindow.Content = controllerImage;
                    string controllerDefaultPath = System.IO.Path.Combine(Program.InstallDir, "default_controller.png");
                    SetImageSourceSafely(controllerImage, controllerDefaultPath);
                    PositionWindowOnMonitor(controllerWindow, 2, "Controller");
                    // Set the image size to match the window's logical dimensions
                    controllerImage.Width = controllerWindow.Width;
                    controllerImage.Height = controllerWindow.Height;
                    controllerWindow.Show();
                    LogToFile("Controller window shown on Monitor 3.");
                }
                else
                {
                    LogToFile("Warning: Only two monitors detected. Controller window will not be displayed.");
                    controllerWindow = null;
                }

                // Ensure the main window retains focus
                this.Activate();
                this.Focus();
                LogToFile("Main window re-activated and focused after setting up secondary windows.");
            }
            catch (Exception ex)
            {
                LogToFile($"Error in SetupMonitors: {ex.Message}");
            }
            LogToFile("Completed SetupMonitors.");
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Close secondary windows to prevent them from lingering
            marqueeWindow?.Close();
            controllerWindow?.Close();
            LogToFile("Closed secondary windows on application shutdown.");
        }
    }
}