using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using ArcadeLauncher.Core;

namespace ArcadeLauncher.SW2
{
    public partial class MainForm : Form
    {
        private void SetupSettingsView()
        {
            mainPanel.Controls.Clear();
            mainPanel.AutoScroll = true; // Enable scrolling to ensure all controls are visible
            mainPanel.Padding = new Padding(0); // Remove any default padding

            // Calculate scaling factor based on screen height (reference: 1080p)
            double scalingFactor = (double)Screen.PrimaryScreen.WorkingArea.Height / 1080;

            // Determine actual input height using an off-screen panel to prevent flicker
            int inputHeight;
            using (var tempPanel = new Panel { Visible = false }) // Off-screen panel
            using (var tempTextBox = new CustomTextBox { Font = largeFont })
            {
                tempPanel.Controls.Add(tempTextBox); // Add to off-screen panel
                inputHeight = tempTextBox.Height; // Get the actual rendered height
            }

            // Calculate the required width for the "Capture" button using an off-screen panel to prevent flicker
            int buttonWidth;
            using (var tempPanel = new Panel { Visible = false }) // Off-screen panel
            using (var tempButton = new CustomButton { Text = "Capture", Font = largeFont })
            {
                tempPanel.Controls.Add(tempButton); // Add to off-screen panel
                buttonWidth = TextRenderer.MeasureText("Capture", largeFont).Width + (int)(15 * scalingFactor); // Add padding for button margins
            }

            // Columnar layout: Label, Input, Buttons (Clear and Capture)
            int labelWidth = (int)(250 * scalingFactor); // Increased from 150 to 250 to prevent clipping
            int inputWidth = (int)(600 * scalingFactor); // Doubled from 300 to 600
            int buttonHeight = inputHeight; // Match button height to input box height
            int labelHeight = inputHeight; // Match label height to input box height
            int columnGap = (int)(10 * scalingFactor);
            int rowHeight = (int)(40 * scalingFactor);
            int currentTop = (int)(10 * scalingFactor);
            int buttonGap = (int)(5 * scalingFactor); // Gap between Clear and Capture buttons

            // Calculate the total width of the columns (Label + Gap + Input + Gap + Clear Button + Gap + Capture Button)
            int totalColumnsWidth = labelWidth + columnGap + inputWidth + columnGap + buttonWidth + buttonGap + buttonWidth;

            // Center the columns by calculating the unused space to the right of the Game List
            int rightOfGameListSpace = Screen.PrimaryScreen.WorkingArea.Width - gameList.Width;
            int unusedSpace = rightOfGameListSpace - totalColumnsWidth;
            int column1Left = gameList.Width + (unusedSpace / 2) - 357; // Restore the 357-pixel adjustment

            // Log the centering calculation
            Logger.LogToFile($"SetupSettingsView Calculations: ScreenWidth={Screen.PrimaryScreen.WorkingArea.Width}, GameListWidth={gameList.Width}, ScalingFactor={scalingFactor}");
            Logger.LogToFile($"SetupSettingsView Calculations: LabelWidth={labelWidth}, InputWidth={inputWidth}, ButtonWidth={buttonWidth}, ButtonHeight={buttonHeight}, ColumnGap={columnGap}, ButtonGap={buttonGap}, TotalColumnsWidth={totalColumnsWidth}");
            Logger.LogToFile($"SetupSettingsView Centering: RightOfGameListSpace={rightOfGameListSpace}, UnusedSpace={unusedSpace}, Column1LeftBeforeAdjustment={gameList.Width + (unusedSpace / 2)}, Column1LeftAfterAdjustment={column1Left}");

            int column2Left = column1Left + labelWidth + columnGap;
            int column3Left = column2Left + inputWidth + columnGap;

            // Number of Columns
            var columnsLabel = new Label { Text = "Number of Columns", Top = currentTop, Left = column1Left, Width = labelWidth, Height = labelHeight, Font = largeFont, TextAlign = ContentAlignment.TopLeft, Padding = new Padding(0) };
            var columnsComboBox = new ComboBox { Top = currentTop, Left = column2Left, Width = inputWidth, Height = inputHeight, DropDownStyle = ComboBoxStyle.DropDownList, Font = largeFont };
            columnsComboBox.Items.AddRange(new object[] { 5, 6, 7, 8, 9 });
            columnsComboBox.SelectedItem = settings.NumberOfColumns > 0 ? settings.NumberOfColumns : 7;
            currentTop += rowHeight;

            // Default Marquee Image
            var marqueeLabel = new Label { Text = "Default Marquee Image", Top = currentTop, Left = column1Left, Width = labelWidth, Height = labelHeight, Font = largeFont, TextAlign = ContentAlignment.TopLeft, Padding = new Padding(0) };
            // Handle case where DefaultMarqueeImage might contain a full path
            string marqueeFileName = settings.DefaultMarqueeImage != null ? Path.GetFileName(settings.DefaultMarqueeImage) : "";
            string initialMarqueePath = string.IsNullOrEmpty(marqueeFileName) ? "" : Path.Combine(ArcadeLauncher.Core.Program.InstallDir, marqueeFileName);
            var marqueeTextBox = new TextBox
            {
                Text = initialMarqueePath,
                Top = currentTop,
                Left = column2Left,
                Width = inputWidth,
                Height = inputHeight,
                Font = largeFont,
                ReadOnly = true // Make read-only to prevent manual path editing
            };
            var marqueeButton = new Button { Text = "Browse", Top = currentTop, Left = column3Left, Width = buttonWidth, Height = buttonHeight, Font = largeFont };
            currentTop += rowHeight;

            // Default Controller Image
            var controllerLabel = new Label { Text = "Default Controller Image", Top = currentTop, Left = column1Left, Width = labelWidth, Height = labelHeight, Font = largeFont, TextAlign = ContentAlignment.TopLeft, Padding = new Padding(0) };
            // Handle case where DefaultControllerImage might contain a full path
            string controllerFileName = settings.DefaultControllerImage != null ? Path.GetFileName(settings.DefaultControllerImage) : "";
            string initialControllerPath = string.IsNullOrEmpty(controllerFileName) ? "" : Path.Combine(ArcadeLauncher.Core.Program.InstallDir, controllerFileName);
            var controllerTextBox = new TextBox
            {
                Text = initialControllerPath,
                Top = currentTop,
                Left = column2Left,
                Width = inputWidth,
                Height = inputHeight,
                Font = largeFont,
                ReadOnly = true // Make read-only to prevent manual path editing
            };
            var controllerButton = new Button { Text = "Browse", Top = currentTop, Left = column3Left, Width = buttonWidth, Height = buttonHeight, Font = largeFont };
            currentTop += rowHeight;

            // Store initial values to detect changes
            string initialMarqueeText = marqueeTextBox.Text;
            string initialControllerText = controllerTextBox.Text;

            // Define Buttons (Left, Right, Up, Down, Select, Exit)
            var buttonLabels = new string[] { "Left", "Right", "Up", "Down", "Select", "Exit" };
            var buttonTextBoxes = new TextBox[buttonLabels.Length];
            var clearButtons = new Button[buttonLabels.Length];
            var captureButtons = new Button[buttonLabels.Length];

            for (int i = 0; i < buttonLabels.Length; i++)
            {
                var buttonLabel = new Label { Text = $"Define {buttonLabels[i]} Button(s)", Top = currentTop, Left = column1Left, Width = labelWidth, Height = labelHeight, Font = largeFont, TextAlign = ContentAlignment.TopLeft, Padding = new Padding(0) };
                var textBox = new TextBox
                {
                    Top = currentTop,
                    Left = column2Left,
                    Width = inputWidth,
                    Height = inputHeight,
                    Font = largeFont,
                    ReadOnly = true
                };
                if (settings.InputMappings.TryGetValue(buttonLabels[i], out var mappings))
                {
                    textBox.Text = String.Join(", ", mappings.ToArray());
                }
                else
                {
                    textBox.Text = "";
                }
                var clearButton = new Button { Text = "Clear", Top = currentTop, Left = column3Left, Width = buttonWidth, Height = buttonHeight, Font = largeFont };
                var captureButton = new Button { Text = "Capture", Top = currentTop, Left = column3Left + buttonWidth + buttonGap, Width = buttonWidth, Height = buttonHeight, Font = largeFont };

                int index = i;
                clearButton.Click += (s, e) =>
                {
                    settings.InputMappings[buttonLabels[index]] = new List<string>();
                    textBox.Text = "";
                };

                captureButton.Click += (s, e) =>
                {
                    var captureForm = new Form
                    {
                        Text = "",
                        Size = new Size((int)(300 * scalingFactor), (int)(150 * scalingFactor)),
                        StartPosition = FormStartPosition.CenterParent,
                        MinimizeBox = false,
                        MaximizeBox = false,
                        ShowIcon = false,
                        BackColor = ColorTranslator.FromHtml("#F3F3F3")
                    };
                    var captureLabel = new Label
                    {
                        Text = "Press a key to capture...",
                        AutoSize = true,
                        TextAlign = ContentAlignment.MiddleCenter,
                        Font = largeFont
                    };
                    captureForm.Controls.Add(captureLabel);

                    captureForm.Load += (sender, args) =>
                    {
                        captureLabel.Left = (captureForm.ClientSize.Width - captureLabel.Width) / 2;
                        int totalHeight = captureForm.Height;
                        int clientHeight = captureForm.ClientSize.Height;
                        int titleBarHeight = SystemInformation.CaptionHeight;
                        int topBorderHeight = SystemInformation.BorderSize.Height;
                        int bottomBorderHeight = SystemInformation.BorderSize.Height;
                        int nonClientHeightAbove = titleBarHeight + topBorderHeight;
                        int nonClientHeightBelow = bottomBorderHeight;
                        int visibleTotalHeight = clientHeight + nonClientHeightAbove + nonClientHeightBelow;
                        int measuredVisibleTotalHeight = 105;
                        int captureLabelHeight = captureLabel.Height;
                        int totalCenterY = measuredVisibleTotalHeight / 2;
                        int labelTop = totalCenterY - (captureLabelHeight / 2);
                        captureLabel.Top = labelTop - nonClientHeightAbove;

                        Logger.LogToFile($"Capture Key Pop-up Centering Diagnostics:");
                        Logger.LogToFile($"  Total Height (including shadow): {totalHeight}");
                        Logger.LogToFile($"  Client Height: {clientHeight}");
                        Logger.LogToFile($"  Title Bar Height: {titleBarHeight}");
                        Logger.LogToFile($"  Top Border Height: {topBorderHeight}");
                        Logger.LogToFile($"  Bottom Border Height: {bottomBorderHeight}");
                        Logger.LogToFile($"  Non-Client Height Above: {nonClientHeightAbove}");
                        Logger.LogToFile($"  Non-Client Height Below: {nonClientHeightBelow}");
                        Logger.LogToFile($"  Calculated Visible Total Height: {visibleTotalHeight}");
                        Logger.LogToFile($"  Measured Visible Total Height: {measuredVisibleTotalHeight}");
                        Logger.LogToFile($"  Label Height: {captureLabelHeight}");
                        Logger.LogToFile($"  Total Center Y (measured): {totalCenterY}");
                        Logger.LogToFile($"  Label Top (before adjustment): {labelTop}");
                        Logger.LogToFile($"  Final Label Top: {captureLabel.Top}");
                    };

                    captureForm.KeyPreview = true;
                    captureForm.KeyDown += (sender, args) =>
                    {
                        string keyString;
                        switch (args.KeyCode)
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
                                keyString = args.KeyCode.ToString();
                                break;
                        }
                        if (!settings.InputMappings[buttonLabels[index]].Contains(keyString, StringComparer.OrdinalIgnoreCase))
                        {
                            settings.InputMappings[buttonLabels[index]].Add(keyString);
                            textBox.Text = String.Join(", ", settings.InputMappings[buttonLabels[index]]);
                        }
                        captureForm.Close();
                    };
                    captureForm.ShowDialog();
                };

                buttonTextBoxes[index] = textBox;
                clearButtons[index] = clearButton;
                captureButtons[index] = captureButton;

                mainPanel.Controls.Add(buttonLabel);
                mainPanel.Controls.Add(textBox);
                mainPanel.Controls.Add(clearButton);
                mainPanel.Controls.Add(captureButton);
                currentTop += rowHeight;
            }

            // Save and Cancel Buttons
            var saveSettingsButton = new Button { Text = "Save Settings", Top = currentTop, Left = column1Left, Width = buttonWidth, Height = buttonHeight, Font = largeFont };
            var cancelSettingsButton = new Button { Text = "Cancel", Top = currentTop, Left = column1Left + buttonWidth + columnGap, Width = buttonWidth, Height = buttonHeight, Font = largeFont };
            saveSettingsButton.Click += (s, e) =>
            {
                try
                {
                    // Update number of columns
                    settings.NumberOfColumns = (int)columnsComboBox.SelectedItem;

                    // Ensure the target directory exists
                    string targetDir = ArcadeLauncher.Core.Program.InstallDir; // Should be C:\ProgramData\ArcadeLauncher
                    if (!Directory.Exists(targetDir))
                    {
                        Directory.CreateDirectory(targetDir);
                        Logger.LogToFile($"Created directory: {targetDir}");
                    }

                    // Copy the default marquee image only if the path has changed
                    if (marqueeTextBox.Text != initialMarqueeText)
                    {
                        if (!string.IsNullOrEmpty(marqueeTextBox.Text) && File.Exists(marqueeTextBox.Text))
                        {
                            string targetMarqueePath = Path.Combine(targetDir, "default_marquee.png");
                            File.Copy(marqueeTextBox.Text, targetMarqueePath, true);
                            settings.DefaultMarqueeImage = "default_marquee.png"; // Store just the file name
                            marqueeTextBox.Text = targetMarqueePath; // Update UI to show the target path
                            Logger.LogToFile($"Copied default marquee image to: {targetMarqueePath}");
                        }
                        else if (!string.IsNullOrEmpty(marqueeTextBox.Text))
                        {
                            Logger.LogToFile($"Default marquee image file not found: {marqueeTextBox.Text}");
                            MessageBox.Show($"Default marquee image file not found: {marqueeTextBox.Text}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }

                    // Copy the default controller image only if the path has changed
                    if (controllerTextBox.Text != initialControllerText)
                    {
                        if (!string.IsNullOrEmpty(controllerTextBox.Text) && File.Exists(controllerTextBox.Text))
                        {
                            string targetControllerPath = Path.Combine(targetDir, "default_controller.png");
                            File.Copy(controllerTextBox.Text, targetControllerPath, true);
                            settings.DefaultControllerImage = "default_controller.png"; // Store just the file name
                            controllerTextBox.Text = targetControllerPath; // Update UI to show the target path
                            Logger.LogToFile($"Copied default controller image to: {targetControllerPath}");
                        }
                        else if (!string.IsNullOrEmpty(controllerTextBox.Text))
                        {
                            Logger.LogToFile($"Default controller image file not found: {controllerTextBox.Text}");
                            MessageBox.Show($"Default controller image file not found: {controllerTextBox.Text}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }

                    // Save settings
                    DataManager.SaveSettings(settings);
                    Logger.LogToFile("Settings saved successfully.");
                    MessageBox.Show("Settings saved successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    SetupSettingsView(); // Stay on Settings Tab after saving
                }
                catch (Exception ex)
                {
                    Logger.LogToFile($"Failed to save settings: {ex.Message}");
                    MessageBox.Show($"Failed to save settings: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            cancelSettingsButton.Click += (s, e) =>
            {
                SetupSettingsView(); // Discard changes and stay on Settings Tab
            };

            marqueeButton.Click += (s, e) =>
            {
                using (var dialog = new OpenFileDialog())
                {
                    dialog.Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp";
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        marqueeTextBox.Text = dialog.FileName;
                        Logger.LogToFile($"Selected default marquee image: {dialog.FileName}");
                    }
                }
            };

            controllerButton.Click += (s, e) =>
            {
                using (var dialog = new OpenFileDialog())
                {
                    dialog.Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp";
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        controllerTextBox.Text = dialog.FileName;
                        Logger.LogToFile($"Selected default controller image: {dialog.FileName}");
                    }
                }
            };

            mainPanel.Controls.Add(columnsLabel);
            mainPanel.Controls.Add(columnsComboBox);
            mainPanel.Controls.Add(marqueeLabel);
            mainPanel.Controls.Add(marqueeTextBox);
            mainPanel.Controls.Add(marqueeButton);
            mainPanel.Controls.Add(controllerLabel);
            mainPanel.Controls.Add(controllerTextBox);
            mainPanel.Controls.Add(controllerButton);
            mainPanel.Controls.Add(saveSettingsButton);
            mainPanel.Controls.Add(cancelSettingsButton);
        }

        private async Task SearchCoverArt(string gameName, PictureBox artBoxPictureBox, Game game)
        {
            try
            {
                Logger.LogToFile($"Starting cover art search for game: {gameName}, Game ID: {gameIds[game]}, DisplayName: {game.DisplayName}");
                // Ensure token is valid (Ticket 02 - Add notification on failure)
                try
                {
                    await EnsureValidTwitchTokenAsync();
                }
                catch (Exception ex)
                {
                    Logger.LogToFile($"Failed to confirm Twitch token: {ex.Message}");
                    MessageBox.Show("Failed to Confirm Token");
                    return;
                }

                // Search for the game on IGDB with a higher limit
                var requestBody = $"fields name, cover.url; search \"{gameName}\"; limit 50;";
                var content = new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync("https://api.igdb.com/v4/games", content);
                response.EnsureSuccessStatusCode();
                var jsonResponse = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                var games = JsonSerializer.Deserialize<List<IGDBGame>>(jsonResponse, options);

                if (games == null || games.Count == 0)
                {
                    Logger.LogToFile("No games found in IGDB response.");
                    MessageBox.Show("No cover art found for this game.");
                    return;
                }

                Logger.LogToFile($"Found {games.Count} games in IGDB response.");
                foreach (var g in games)
                {
                    Logger.LogToFile($"Game: {g.Name}, Cover URL: {(g.cover != null ? g.cover.Url : "null")}");
                }

                // Show a borderless form with cover art options (non-modal)
                var tcs = new TaskCompletionSource<string>();
                using (var coverArtForm = new CoverArtSelectionForm(games, httpClient))
                {
                    coverArtForm.FormClosed += (s, e) => tcs.TrySetResult(coverArtForm.SelectedCoverUrl);
                    coverArtForm.Deactivate += (s, e) =>
                    {
                        Logger.LogToFile("CoverArtSelectionForm lost focus. Closing form.");
                        coverArtForm.Close();
                    };
                    coverArtForm.Show(); // Use Show() instead of ShowDialog() to allow clicking outside

                    // Wait for the form to close and get the result
                    var selectedCoverUrl = await tcs.Task;

                    if (!string.IsNullOrEmpty(selectedCoverUrl))
                    {
                        Logger.LogToFile($"Selected cover art URL: {selectedCoverUrl}");
                        // Download the selected cover art in high resolution (t_1080p)
                        var highResUrl = selectedCoverUrl.Replace("t_thumb", "t_1080p");
                        Logger.LogToFile($"Downloading high-res image from: {highResUrl}");
                        var imageBytes = await httpClient.GetByteArrayAsync($"https:{highResUrl}");
                        var tempPath = Path.Combine(Path.GetTempPath(), "tempCoverArt.png");
                        Logger.LogToFile($"Saving temporary file to: {tempPath}");
                        File.WriteAllBytes(tempPath, imageBytes);

                        // Sanitize the DisplayName for the file path and include the Game ID for uniqueness
                        string sanitizedDisplayName = game.DisplayName;
                        foreach (char invalidChar in Path.GetInvalidFileNameChars())
                        {
                            sanitizedDisplayName = sanitizedDisplayName.Replace(invalidChar, '_');
                        }
                        string uniqueFolderName = $"{sanitizedDisplayName}_{gameIds[game]}"; // Append Game ID to folder name
                        var gameDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ArcadeLauncher", "Assets", uniqueFolderName);
                        Logger.LogToFile($"Creating directory for game assets: {gameDir}");
                        Directory.CreateDirectory(gameDir);
                        var destPath = Path.Combine(gameDir, "ArtBox.png");
                        Logger.LogToFile($"Destination path for cover art: {destPath}");

                        // Dispose of the current image in the PictureBox
                        if (artBoxPictureBox.Image != null)
                        {
                            Logger.LogToFile("Disposing of current PictureBox image.");
                            artBoxPictureBox.Image.Dispose();
                            artBoxPictureBox.Image = null;
                            // Force garbage collection to release file handles
                            GC.Collect();
                            GC.WaitForPendingFinalizers();
                        }

                        // Copy with retry mechanism
                        bool copied = false;
                        for (int i = 0; i < 5 && !copied; i++)
                        {
                            try
                            {
                                Logger.LogToFile($"Attempt {i + 1} to copy image from {tempPath} to {destPath}");
                                File.Copy(tempPath, destPath, true);
                                copied = true;
                                Logger.LogToFile($"Successfully copied image to: {destPath}");
                            }
                            catch (IOException ex)
                            {
                                if (i == 4) // Last attempt failed
                                {
                                    Logger.LogToFile($"Failed to copy image after multiple attempts: {ex.Message}");
                                    MessageBox.Show($"Failed to fetch cover art: {ex.Message}");
                                    return;
                                }
                                Logger.LogToFile($"Copy attempt {i + 1} failed: {ex.Message}. Retrying after 500ms...");
                                await Task.Delay(500); // Wait 500ms before retrying
                            }
                        }

                        // Load the new image into the PictureBox
                        Logger.LogToFile($"Loading new image into PictureBox from: {destPath}");
                        artBoxPictureBox.Image = Image.FromFile(destPath);
                        game.ArtBoxPath = destPath; // Update the game's ArtBoxPath
                        Logger.LogToFile($"Updated game ArtBoxPath: {game.ArtBoxPath}");
                    }
                    else
                    {
                        Logger.LogToFile("Cover art selection cancelled or no cover selected.");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogToFile($"Failed to fetch cover art: {ex.Message}");
                MessageBox.Show($"Failed to fetch cover art: {ex.Message}");
            }
        }

        private void SelectImage(PictureBox pictureBox, string assetType, Game game)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp";
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    Logger.LogToFile($"Selecting image for {assetType}, Game ID: {gameIds[game]}, DisplayName: {game.DisplayName}, File: {dialog.FileName}");
                    // Sanitize the DisplayName for the file path and include the Game ID for uniqueness
                    string sanitizedDisplayName = game.DisplayName;
                    foreach (char invalidChar in Path.GetInvalidFileNameChars())
                    {
                        sanitizedDisplayName = sanitizedDisplayName.Replace(invalidChar, '_');
                    }
                    string uniqueFolderName = $"{sanitizedDisplayName}_{gameIds[game]}"; // Append Game ID to folder name
                    var gameDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ArcadeLauncher", "Assets", uniqueFolderName);
                    Logger.LogToFile($"Creating directory for game assets: {gameDir}");
                    Directory.CreateDirectory(gameDir);
                    var destPath = Path.Combine(gameDir, $"{assetType}.png");
                    Logger.LogToFile($"Destination path for {assetType}: {destPath}");

                    // Dispose of the current image in the PictureBox
                    if (pictureBox.Image != null)
                    {
                        Logger.LogToFile("Disposing of current PictureBox image.");
                        pictureBox.Image.Dispose();
                        pictureBox.Image = null;
                    }

                    // Copy with retry mechanism
                    bool copied = false;
                    for (int i = 0; i < 5 && !copied; i++)
                    {
                        try
                        {
                            Logger.LogToFile($"Attempt {i + 1} to copy image from {dialog.FileName} to {destPath}");
                            File.Copy(dialog.FileName, destPath, true);
                            copied = true;
                            Logger.LogToFile($"Successfully copied image to: {destPath}");
                        }
                        catch (IOException ex)
                        {
                            if (i == 4) // Last attempt failed
                            {
                                Logger.LogToFile($"Failed to copy image after multiple attempts: {ex.Message}");
                                MessageBox.Show($"Failed to copy image after multiple attempts: {ex.Message}");
                                return;
                            }
                            Logger.LogToFile($"Copy attempt {i + 1} failed: {ex.Message}. Retrying after 500ms...");
                            System.Threading.Thread.Sleep(500); // Wait 500ms before retrying
                        }
                    }

                    // Load the new image into the PictureBox
                    Logger.LogToFile($"Loading new image into PictureBox from: {destPath}");
                    pictureBox.Image = Image.FromFile(destPath);

                    // Update the game's paths
                    if (assetType == "ArtBox")
                    {
                        game.ArtBoxPath = destPath;
                        Logger.LogToFile($"Updated game ArtBoxPath: {game.ArtBoxPath}");
                    }
                    else if (assetType == "Marquee")
                    {
                        game.MarqueePath = destPath;
                        Logger.LogToFile($"Updated game MarqueePath: {game.MarqueePath}");
                    }
                    else if (assetType == "ControllerLayout")
                    {
                        game.ControllerLayoutPath = destPath;
                        Logger.LogToFile($"Updated game ControllerLayoutPath: {game.ControllerLayoutPath}");
                    }
                }
            }
        }

        // Class to represent IGDB game data
        public class IGDBGame
        {
            public string Name { get; set; }
            public IGDBCover cover { get; set; }
        }

        public class IGDBCover
        {
            public string Url { get; set; }
        }

        // Form to display cover art options
        public class CoverArtSelectionForm : Form
        {
            private List<IGDBGame> games;
            private List<PictureBox> pictureBoxes;
            private HttpClient httpClient;
            private FlowLayoutPanel flowLayoutPanel;
            private Label loadingLabel;
            public string SelectedCoverUrl { get; private set; }

            public CoverArtSelectionForm(List<IGDBGame> games, HttpClient httpClient)
            {
                this.games = games;
                this.httpClient = httpClient;

                // Calculate scaling factor based on screen height (reference: 1080p)
                double scalingFactor = (double)Screen.PrimaryScreen.WorkingArea.Height / 1080;
                Logger.LogToFile($"CoverArtSelectionForm Scaling Factor: scalingFactor={scalingFactor}, ScreenHeight={Screen.PrimaryScreen.WorkingArea.Height}");

                // Scale the window width, height, thumbnail size, gaps, and margins
                int thumbnailWidth = (int)(200 * scalingFactor); // New base width: 200px (scaled from 264px)
                int thumbnailHeight = (int)(thumbnailWidth * 4.0 / 3.0); // Maintain 4:3 aspect ratio
                int gap = (int)(5 * scalingFactor); // 5px gap at 1080p
                int columns = 5; // Target 5 columns
                int margin = (int)(18 * scalingFactor); // 18px margin on each side at 1080p
                int scrollbarWidth = (int)(20 * scalingFactor); // 20px scrollbar width at 1080p (approximate)
                int formWidth = (columns * thumbnailWidth) + ((columns - 1) * gap) + (2 * margin) + scrollbarWidth + (int)(10 * scalingFactor) + 25; // Add 25 pixels for right margin and comfort
                int formHeight = (int)(500 * scalingFactor); // 500px height at 1080p (adjustable with AutoScroll)

                // Set the main form properties
                this.Size = new Size(formWidth, formHeight);
                this.StartPosition = FormStartPosition.CenterScreen; // Center on the screen
                this.BackColor = ColorTranslator.FromHtml("#F3F3F3"); // Set background to grey (#F3F3F3)
                this.FormBorderStyle = FormBorderStyle.FixedSingle;
                this.ControlBox = true; // Keep the close button
                this.MinimizeBox = false; // Remove minimize button
                this.MaximizeBox = false; // Remove maximize button
                this.ShowIcon = false; // Remove the icon
                this.Text = "Select Cover Art"; // Set title
                pictureBoxes = new List<PictureBox>();

                // Add a loading label
                loadingLabel = new Label
                {
                    Text = "Loading cover art...",
                    ForeColor = Color.White,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Microsoft Sans Serif", (float)(12 * scalingFactor))
                };
                this.Controls.Add(loadingLabel);

                // Add FlowLayoutPanel for thumbnails
                flowLayoutPanel = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    AutoScroll = true,
                    Padding = new Padding(margin, margin, margin, margin) // Apply margin to all sides
                };
                this.Controls.Add(flowLayoutPanel);
                flowLayoutPanel.Visible = false; // Hide until images load

                // Load images after the form is shown
                this.Load += async (s, e) =>
                {
                    await LoadCoverArtImagesAsync(thumbnailWidth, gap);
                    loadingLabel.Visible = false;
                    flowLayoutPanel.Visible = true;
                };
            }

            private async Task LoadCoverArtImagesAsync(int thumbnailWidth, int gap)
            {
                Logger.LogToFile($"Loading cover art images for {games.Count} games.");
                foreach (var game in games.Where(g => g.cover != null && !string.IsNullOrEmpty(g.cover.Url)))
                {
                    try
                    {
                        Logger.LogToFile($"Processing game: {game.Name}, Cover URL: {game.cover.Url}");
                        // Use a higher resolution image for the selection form (t_cover_big)
                        var coverUrl = game.cover.Url.Replace("t_thumb", "t_cover_big");
                        Logger.LogToFile($"Fetching image from: {coverUrl}");
                        var imageBytes = await httpClient.GetByteArrayAsync($"https:{coverUrl}");
                        Logger.LogToFile($"Successfully fetched image for {game.Name}, size: {imageBytes.Length} bytes");
                        using (var ms = new MemoryStream(imageBytes))
                        {
                            var pictureBox = new PictureBox
                            {
                                Size = new Size(thumbnailWidth, (int)(thumbnailWidth * 4.0 / 3.0)), // Maintain 3:4 aspect ratio
                                SizeMode = PictureBoxSizeMode.StretchImage,
                                Image = Image.FromStream(ms),
                                BorderStyle = BorderStyle.FixedSingle,
                                BackColor = Color.Black,
                                Margin = new Padding(gap / 2) // Half the gap on each side to achieve total gap between thumbnails
                            };
                            pictureBox.Click += (s, e) =>
                            {
                                SelectedCoverUrl = game.cover.Url;
                                Logger.LogToFile($"Selected cover art for {game.Name}: {SelectedCoverUrl}");
                                this.Close();
                            };
                            pictureBoxes.Add(pictureBox);
                            flowLayoutPanel.Controls.Add(pictureBox);
                            Logger.LogToFile($"Added picture box for {game.Name}");
                        }
                    }
                    catch (Exception ex)
                    {
                        // Skip failed images
                        Logger.LogToFile($"Failed to load cover art for {game.Name}: {ex.Message}");
                    }
                }

                Logger.LogToFile($"Finished loading cover art images. Total images loaded: {pictureBoxes.Count}");
                // If no images were loaded, show a message and close the form
                if (pictureBoxes.Count == 0)
                {
                    Logger.LogToFile("No cover art images were loaded. Closing form.");
                    MessageBox.Show("No cover art images could be loaded.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    this.Close();
                }
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    foreach (var pictureBox in pictureBoxes)
                    {
                        if (pictureBox.Image != null)
                        {
                            pictureBox.Image.Dispose();
                            pictureBox.Image = null;
                        }
                        pictureBox.Dispose();
                    }
                    pictureBoxes.Clear();
                }
                base.Dispose(disposing);
            }
        }
    }
}