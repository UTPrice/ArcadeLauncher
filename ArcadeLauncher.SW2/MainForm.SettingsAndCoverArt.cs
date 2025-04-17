using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
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
            mainPanel.AutoScroll = true;
            mainPanel.Padding = new Padding(0);

            double scalingFactor = (double)Screen.PrimaryScreen.WorkingArea.Height / 1080;

            int inputHeight;
            using (var tempPanel = new Panel { Visible = false })
            using (var tempTextBox = new CustomTextBox { Font = largeFont })
            {
                tempPanel.Controls.Add(tempTextBox);
                inputHeight = tempTextBox.Height;
            }

            int buttonWidth;
            using (var tempPanel = new Panel { Visible = false })
            using (var tempButton = new CustomButton { Text = "Capture", Font = largeFont })
            {
                tempPanel.Controls.Add(tempButton);
                buttonWidth = TextRenderer.MeasureText("Capture", largeFont).Width + (int)(15 * scalingFactor);
            }

            int labelWidth = (int)(250 * scalingFactor);
            int inputWidth = (int)(600 * scalingFactor);
            int buttonHeight = inputHeight;
            int labelHeight = inputHeight;
            int columnGap = (int)(10 * scalingFactor);
            int rowHeight = (int)(40 * scalingFactor);
            int currentTop = (int)(10 * scalingFactor);
            int buttonGap = (int)(5 * scalingFactor);

            int totalColumnsWidth = labelWidth + columnGap + inputWidth + columnGap + buttonWidth + buttonGap + buttonWidth;
            int rightOfGameListSpace = Screen.PrimaryScreen.WorkingArea.Width - gameList.Width;
            int unusedSpace = rightOfGameListSpace - totalColumnsWidth;
            int column1Left = gameList.Width + (unusedSpace / 2) - 357;

            Logger.LogToFile($"SetupSettingsView Calculations: ScreenWidth={Screen.PrimaryScreen.WorkingArea.Width}, GameListWidth={gameList.Width}, ScalingFactor={scalingFactor}");
            Logger.LogToFile($"SetupSettingsView Calculations: LabelWidth={labelWidth}, InputWidth={inputWidth}, ButtonWidth={buttonWidth}, ButtonHeight={buttonHeight}, ColumnGap={columnGap}, ButtonGap={buttonGap}, TotalColumnsWidth={totalColumnsWidth}");
            Logger.LogToFile($"SetupSettingsView Centering: RightOfGameListSpace={rightOfGameListSpace}, UnusedSpace={unusedSpace}, Column1LeftBeforeAdjustment={gameList.Width + (unusedSpace / 2)}, Column1LeftAfterAdjustment={column1Left}");

            int column2Left = column1Left + labelWidth + columnGap;
            int column3Left = column2Left + inputWidth + columnGap;

            var columnsLabel = new Label { Text = "Number of Columns", Top = currentTop, Left = column1Left, Width = labelWidth, Height = labelHeight, Font = largeFont, TextAlign = ContentAlignment.TopLeft, Padding = new Padding(0) };
            var columnsComboBox = new ComboBox { Top = currentTop, Left = column2Left, Width = inputWidth, Height = inputHeight, DropDownStyle = ComboBoxStyle.DropDownList, Font = largeFont };
            columnsComboBox.Items.AddRange(new object[] { 5, 6, 7, 8, 9 });
            columnsComboBox.SelectedItem = settings.NumberOfColumns > 0 ? settings.NumberOfColumns : 7;
            currentTop += rowHeight;

            var marqueeLabel = new Label { Text = "Default Marquee Image", Top = currentTop, Left = column1Left, Width = labelWidth, Height = labelHeight, Font = largeFont, TextAlign = ContentAlignment.TopLeft, Padding = new Padding(0) };
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
                ReadOnly = true
            };
            var marqueeButton = new Button { Text = "Browse", Top = currentTop, Left = column3Left, Width = buttonWidth, Height = buttonHeight, Font = largeFont };
            currentTop += rowHeight;

            var controllerLabel = new Label { Text = "Default Controller Image", Top = currentTop, Left = column1Left, Width = labelWidth, Height = labelHeight, Font = largeFont, TextAlign = ContentAlignment.TopLeft, Padding = new Padding(0) };
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
                ReadOnly = true
            };
            var controllerButton = new Button { Text = "Browse", Top = currentTop, Left = column3Left, Width = buttonWidth, Height = buttonHeight, Font = largeFont };
            currentTop += rowHeight;

            string initialMarqueeText = marqueeTextBox.Text;
            string initialControllerText = controllerTextBox.Text;

            var buttonLabels = new string[] { "Left", "Right", "Up", "Down", "Select", "Exit", "Kill" };
            var buttonTextBoxes = new TextBox[buttonLabels.Length];
            var clearButtons = new Button[buttonLabels.Length];
            var captureButtons = new Button[buttonLabels.Length];

            for (int i = 0; i < buttonLabels.Length; i++)
            {
                var buttonLabel = new Label { Text = $"Define {buttonLabels[i]} Button(s)", Top = currentTop, Left = column1Left, Width = labelWidth, Height = labelHeight, Font = largeFont, TextAlign = ContentAlignment.TopLeft, Padding = new Padding(0) };
                buttonTextBoxes[i] = new TextBox
                {
                    Top = currentTop,
                    Left = column2Left,
                    Width = inputWidth,
                    Height = inputHeight,
                    Font = largeFont,
                    ReadOnly = true
                };
                if (!settings.InputMappings.ContainsKey(buttonLabels[i]))
                {
                    settings.InputMappings[buttonLabels[i]] = new List<string>();
                    Logger.LogToFile($"Initialized missing key in InputMappings: {buttonLabels[i]}");
                }
                if (settings.InputMappings.TryGetValue(buttonLabels[i], out var mappings))
                {
                    buttonTextBoxes[i].Text = String.Join(", ", mappings.ToArray());
                }
                else
                {
                    buttonTextBoxes[i].Text = "";
                    Logger.LogToFile($"Failed to retrieve mappings for key: {buttonLabels[i]}");
                }
                clearButtons[i] = new Button { Text = "Clear", Top = currentTop, Left = column3Left, Width = buttonWidth, Height = buttonHeight, Font = largeFont };
                captureButtons[i] = new Button { Text = "Capture", Top = currentTop, Left = column3Left + buttonWidth + buttonGap, Width = buttonWidth, Height = buttonHeight, Font = largeFont };

                int index = i;
                clearButtons[i].Click += (s, e) =>
                {
                    try
                    {
                        settings.InputMappings[buttonLabels[index]] = new List<string>();
                        buttonTextBoxes[index].Text = "";
                        Logger.LogToFile($"Cleared mappings for {buttonLabels[index]}");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogToFile($"Error clearing mappings for {buttonLabels[index]}: {ex.Message}");
                        MessageBox.Show($"Error clearing mappings: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                };

                captureButtons[i].Click += (s, e) =>
                {
                    try
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
                                buttonTextBoxes[index].Text = String.Join(", ", settings.InputMappings[buttonLabels[index]]);
                                Logger.LogToFile($"Captured key {keyString} for {buttonLabels[index]}");
                            }
                            captureForm.Close();
                        };
                        captureForm.ShowDialog();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogToFile($"Error capturing key for {buttonLabels[index]}: {ex.Message}");
                        MessageBox.Show($"Error capturing key: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                };

                mainPanel.Controls.Add(buttonLabel);
                mainPanel.Controls.Add(buttonTextBoxes[i]);
                mainPanel.Controls.Add(clearButtons[i]);
                mainPanel.Controls.Add(captureButtons[i]);
                currentTop += rowHeight;
            }

            var killLabel = mainPanel.Controls.OfType<Label>().FirstOrDefault(l => l.Text == "Define Kill Button(s)");
            if (killLabel != null)
            {
                killLabel.Text = "Define Kill Button(s) (x2)";
            }

            var saveSettingsButton = new Button { Text = "Save Settings", Top = currentTop, Left = column1Left, Width = buttonWidth, Height = buttonHeight, Font = largeFont };
            var cancelSettingsButton = new Button { Text = "Cancel", Top = currentTop, Left = column1Left + buttonWidth + columnGap, Width = buttonWidth, Height = buttonHeight, Font = largeFont };
            saveSettingsButton.Click += (s, e) =>
            {
                try
                {
                    settings.NumberOfColumns = (int)columnsComboBox.SelectedItem;

                    string targetDir = ArcadeLauncher.Core.Program.InstallDir;
                    if (!Directory.Exists(targetDir))
                    {
                        Directory.CreateDirectory(targetDir);
                        Logger.LogToFile($"Created directory: {targetDir}");
                    }

                    if (marqueeTextBox.Text != initialMarqueeText)
                    {
                        if (!string.IsNullOrEmpty(marqueeTextBox.Text) && File.Exists(marqueeTextBox.Text))
                        {
                            string targetMarqueePath = Path.Combine(targetDir, "default_marquee.png");
                            File.Copy(marqueeTextBox.Text, targetMarqueePath, true);
                            settings.DefaultMarqueeImage = "default_marquee.png";
                            marqueeTextBox.Text = targetMarqueePath;
                            Logger.LogToFile($"Copied default marquee image to: {targetMarqueePath}");
                        }
                        else if (!string.IsNullOrEmpty(marqueeTextBox.Text))
                        {
                            Logger.LogToFile($"Default marquee image file not found: {marqueeTextBox.Text}");
                            MessageBox.Show($"Default marquee image file not found: {marqueeTextBox.Text}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }

                    if (controllerTextBox.Text != initialControllerText)
                    {
                        if (!string.IsNullOrEmpty(controllerTextBox.Text) && File.Exists(controllerTextBox.Text))
                        {
                            string targetControllerPath = Path.Combine(targetDir, "default_controller.png");
                            File.Copy(controllerTextBox.Text, targetControllerPath, true);
                            settings.DefaultControllerImage = "default_controller.png";
                            controllerTextBox.Text = targetControllerPath;
                            Logger.LogToFile($"Copied default controller image to: {targetControllerPath}");
                        }
                        else if (!string.IsNullOrEmpty(controllerTextBox.Text))
                        {
                            Logger.LogToFile($"Default controller image file not found: {controllerTextBox.Text}");
                            MessageBox.Show($"Default controller image file not found: {controllerTextBox.Text}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }

                    DataManager.SaveSettings(settings);
                    Logger.LogToFile("Settings saved successfully.");
                    MessageBox.Show("Settings saved successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    SetupSettingsView();
                }
                catch (Exception ex)
                {
                    Logger.LogToFile($"Failed to save settings: {ex.Message}");
                    MessageBox.Show($"Failed to save settings: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            cancelSettingsButton.Click += (s, e) =>
            {
                SetupSettingsView();
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
            foreach (var textBox in buttonTextBoxes)
            {
                mainPanel.Controls.Add(textBox);
            }
            foreach (var clearButton in clearButtons)
            {
                mainPanel.Controls.Add(clearButton);
            }
            foreach (var captureButton in captureButtons)
            {
                mainPanel.Controls.Add(captureButton);
            }
        }

        private async Task SearchCoverArt(string gameName, PictureBox artBoxPictureBox, Game game, string assetType = "ArtBox")
        {
            try
            {
                Logger.LogToFile($"Starting image search for {assetType}, Game: {gameName}, Game ID: {gameIds[game]}, DisplayName: {game.DisplayName}");
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

                string requestBody;
                if (assetType == "SplashScreen")
                {
                    requestBody = $"fields name,screenshots.url,artworks.url; search \"{gameName}\"; limit 40;";
                }
                else
                {
                    requestBody = $"fields name,cover.url; search \"{gameName}\"; limit 40;";
                }

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
                    MessageBox.Show($"No {assetType.ToLower()} found for this game.");
                    return;
                }

                Logger.LogToFile($"Found {games.Count} games in IGDB response.");
                foreach (var g in games)
                {
                    if (assetType == "SplashScreen")
                    {
                        Logger.LogToFile($"Game: {g.Name}, Artwork URLs: {(g.Artworks != null ? string.Join(", ", g.Artworks.Select(a => a.Url)) : "null")}, Screenshot URLs: {(g.Screenshots != null ? string.Join(", ", g.Screenshots.Select(s => s.Url)) : "null")}");
                    }
                    else
                    {
                        Logger.LogToFile($"Game: {g.Name}, Cover URL: {(g.Cover != null ? g.Cover.Url : "null")}");
                    }
                }

                var tcs = new TaskCompletionSource<string>();
                using (var coverArtForm = new CoverArtSelectionForm(games, httpClient, assetType))
                {
                    coverArtForm.FormClosed += (s, e) => tcs.TrySetResult(coverArtForm.SelectedCoverUrl);
                    coverArtForm.Deactivate += (s, e) =>
                    {
                        Logger.LogToFile("CoverArtSelectionForm lost focus. Closing form.");
                        coverArtForm.Close();
                    };
                    // Load images and show the form
                    await coverArtForm.LoadAndShowAsync();

                    var selectedCoverUrl = await tcs.Task;

                    if (!string.IsNullOrEmpty(selectedCoverUrl))
                    {
                        Logger.LogToFile($"Selected {assetType.ToLower()} URL: {selectedCoverUrl}");

                        // Dispose of the current image in the PictureBox to release file handles
                        if (artBoxPictureBox.Image != null)
                        {
                            Logger.LogToFile("Disposing of current PictureBox image to release file handles.");
                            artBoxPictureBox.Image.Dispose();
                            artBoxPictureBox.Image = null;
                            // Force garbage collection to ensure file handles are released
                            GC.Collect();
                            GC.WaitForPendingFinalizers();
                        }

                        string highResUrl;
                        byte[] imageBytes = null;
                        if (assetType == "SplashScreen")
                        {
                            // Prioritize t_1080p, then t_4k, then t_screenshot_big
                            highResUrl = selectedCoverUrl.Replace("t_thumb", "t_1080p");
                            Logger.LogToFile($"Attempting to fetch t_1080p image from: {highResUrl}");
                            try
                            {
                                imageBytes = await httpClient.GetByteArrayAsync($"https:{highResUrl}");
                                using (var ms = new MemoryStream(imageBytes))
                                using (var tempImage = Image.FromStream(ms))
                                {
                                    Logger.LogToFile($"Successfully fetched t_1080p image with dimensions: {tempImage.Width}x{tempImage.Height}");
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.LogToFile($"Failed to fetch t_1080p image: {ex.Message}");
                                highResUrl = selectedCoverUrl.Replace("t_thumb", "t_4k");
                                Logger.LogToFile($"Falling back to t_4k: {highResUrl}");
                                try
                                {
                                    imageBytes = await httpClient.GetByteArrayAsync($"https:{highResUrl}");
                                    using (var ms = new MemoryStream(imageBytes))
                                    using (var tempImage = Image.FromStream(ms))
                                    {
                                        Logger.LogToFile($"Successfully fetched t_4k image with dimensions: {tempImage.Width}x{tempImage.Height}");
                                    }
                                }
                                catch (Exception ex2)
                                {
                                    Logger.LogToFile($"Failed to fetch t_4k image: {ex2.Message}");
                                    highResUrl = selectedCoverUrl.Replace("t_thumb", "t_screenshot_big");
                                    Logger.LogToFile($"Falling back to t_screenshot_big: {highResUrl}");
                                    try
                                    {
                                        imageBytes = await httpClient.GetByteArrayAsync($"https:{highResUrl}");
                                        using (var ms = new MemoryStream(imageBytes))
                                        using (var tempImage = Image.FromStream(ms))
                                        {
                                            Logger.LogToFile($"Successfully fetched t_screenshot_big image with dimensions: {tempImage.Width}x{tempImage.Height}");
                                        }
                                    }
                                    catch (Exception ex3)
                                    {
                                        Logger.LogToFile($"Failed to fetch t_screenshot_big image: {ex3.Message}");
                                        highResUrl = selectedCoverUrl;
                                        Logger.LogToFile($"Falling back to original URL: {highResUrl}");
                                        imageBytes = await httpClient.GetByteArrayAsync($"https:{highResUrl}");
                                        using (var ms = new MemoryStream(imageBytes))
                                        using (var tempImage = Image.FromStream(ms))
                                        {
                                            Logger.LogToFile($"Successfully fetched original image with dimensions: {tempImage.Width}x{tempImage.Height}");
                                        }
                                    }
                                }
                            }
                            Logger.LogToFile($"Downloaded image size: {imageBytes.Length} bytes");
                        }
                        else
                        {
                            highResUrl = selectedCoverUrl.Replace("t_thumb", "t_1080p");
                            Logger.LogToFile($"Fetching t_1080p image for cover: {highResUrl}");
                            imageBytes = await httpClient.GetByteArrayAsync($"https:{highResUrl}");
                            Logger.LogToFile($"Downloaded image size: {imageBytes.Length} bytes");
                        }

                        // Load the image to get its dimensions
                        using (var ms = new MemoryStream(imageBytes))
                        using (var tempImage = Image.FromStream(ms))
                        {
                            Logger.LogToFile($"Final downloaded image dimensions before resizing: {tempImage.Width}x{tempImage.Height}");
                            if (tempImage.Width < 1920 || tempImage.Height < 1080)
                            {
                                Logger.LogToFile($"Warning: Downloaded image is lower resolution than expected (1920x1080).");
                            }
                        }

                        var tempPath = Path.Combine(Path.GetTempPath(), $"temp{assetType}.png");
                        Logger.LogToFile($"Saving temporary file to: {tempPath}");
                        File.WriteAllBytes(tempPath, imageBytes);

                        string sanitizedDisplayName = game.DisplayName;
                        foreach (char invalidChar in Path.GetInvalidFileNameChars())
                        {
                            sanitizedDisplayName = sanitizedDisplayName.Replace(invalidChar, '_');
                        }
                        string uniqueFolderName = $"{sanitizedDisplayName}_{gameIds[game]}";
                        var gameDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ArcadeLauncher", "Assets", uniqueFolderName);
                        Logger.LogToFile($"Creating directory for game assets: {gameDir}");
                        Directory.CreateDirectory(gameDir);

                        if (assetType == "SplashScreen")
                        {
                            // Generate 4K, 1440p, and 1080p variants
                            using (var tempImage = Image.FromFile(tempPath))
                            using (var sourceImage = new Bitmap(tempImage))
                            {
                                // 4K (3840x2160) - Always generate, even if source is smaller
                                var destPath4k = Path.Combine(gameDir, "SplashScreen_4k.png");
                                using (var resizedImage = new Bitmap(3840, 2160))
                                using (var graphics4k = Graphics.FromImage(resizedImage))
                                {
                                    graphics4k.InterpolationMode = InterpolationMode.HighQualityBicubic;
                                    graphics4k.DrawImage(sourceImage, 0, 0, 3840, 2160);
                                    resizedImage.Save(destPath4k, System.Drawing.Imaging.ImageFormat.Png);
                                    Logger.LogToFile($"Saved 4K splash screen image to: {destPath4k} with dimensions: {resizedImage.Width}x{resizedImage.Height}");
                                }
                                game.SplashScreenPath["4k"] = destPath4k;
                                Logger.LogToFile($"Saved 4K splash screen image to: {destPath4k}");
                                if (sourceImage.Width < 3840 || sourceImage.Height < 2160)
                                {
                                    Logger.LogToFile($"Warning: 4K variant upscaled from {sourceImage.Width}x{sourceImage.Height}, quality may be suboptimal.");
                                }

                                // 1440p (2560x1440)
                                var destPath1440p = Path.Combine(gameDir, "SplashScreen_1440p.png");
                                using (var resizedImage = new Bitmap(2560, 1440))
                                using (var graphics1440p = Graphics.FromImage(resizedImage))
                                {
                                    graphics1440p.InterpolationMode = InterpolationMode.HighQualityBicubic;
                                    graphics1440p.DrawImage(sourceImage, 0, 0, 2560, 1440);
                                    resizedImage.Save(destPath1440p, System.Drawing.Imaging.ImageFormat.Png);
                                    Logger.LogToFile($"Saved 1440p splash screen image to: {destPath1440p} with dimensions: {resizedImage.Width}x{resizedImage.Height}");
                                }
                                game.SplashScreenPath["1440p"] = destPath1440p;
                                Logger.LogToFile($"Saved 1440p splash screen image to: {destPath1440p}");

                                // 1080p (1920x1080)
                                var destPath1080p = Path.Combine(gameDir, "SplashScreen_1080p.png");
                                using (var resizedImage = new Bitmap(1920, 1080))
                                using (var graphics1080p = Graphics.FromImage(resizedImage))
                                {
                                    graphics1080p.InterpolationMode = InterpolationMode.HighQualityBicubic;
                                    graphics1080p.DrawImage(sourceImage, 0, 0, 1920, 1080);
                                    resizedImage.Save(destPath1080p, System.Drawing.Imaging.ImageFormat.Png);
                                    Logger.LogToFile($"Saved 1080p splash screen image to: {destPath1080p} with dimensions: {resizedImage.Width}x{resizedImage.Height}");
                                }
                                game.SplashScreenPath["1080p"] = destPath1080p;
                                Logger.LogToFile($"Saved 1080p splash screen image to: {destPath1080p}");
                            }

                            // Load the appropriate resolution into the PictureBox (1440p preferred)
                            string displayPath = game.SplashScreenPath.ContainsKey("1440p") ? game.SplashScreenPath["1440p"] : game.SplashScreenPath["1080p"];
                            Logger.LogToFile($"Loading splash screen image into PictureBox from: {displayPath}");
                            using (var stream = new FileStream(displayPath, FileMode.Open, FileAccess.Read))
                            {
                                artBoxPictureBox.Image = Image.FromStream(stream);
                            }
                        }
                        else
                        {
                            var destPath = Path.Combine(gameDir, $"{assetType}.png");
                            Logger.LogToFile($"Destination path for {assetType}: {destPath}");

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
                                    if (i == 4)
                                    {
                                        Logger.LogToFile($"Failed to copy image after multiple attempts: {ex.Message}");
                                        MessageBox.Show($"Failed to fetch {assetType.ToLower()}: {ex.Message}");
                                        return;
                                    }
                                    Logger.LogToFile($"Copy attempt {i + 1} failed: {ex.Message}. Retrying after 500ms...");
                                    await Task.Delay(500);
                                }
                            }

                            if (assetType == "Marquee")
                            {
                                var previewPath = Path.Combine(gameDir, $"{assetType}Preview.png");
                                using (var tempImage = Image.FromFile(destPath))
                                using (var sourceImage = new Bitmap(tempImage))
                                {
                                    Rectangle cropRect = new Rectangle(0, 0, 1920, 360);
                                    using (var croppedBitmap = new Bitmap(1920, 360))
                                    using (var graphicsPreview = Graphics.FromImage(croppedBitmap))
                                    {
                                        graphicsPreview.InterpolationMode = InterpolationMode.HighQualityBicubic;
                                        graphicsPreview.DrawImage(sourceImage, new Rectangle(0, 0, 1920, 360), cropRect, GraphicsUnit.Pixel);
                                        croppedBitmap.Save(previewPath, System.Drawing.Imaging.ImageFormat.Png);
                                        Logger.LogToFile($"Saved cropped preview image to: {previewPath} with dimensions: {croppedBitmap.Width}x{croppedBitmap.Height}");
                                    }
                                }
                                Logger.LogToFile($"Loading preview image into PictureBox from: {previewPath}");
                                using (var stream = new FileStream(previewPath, FileMode.Open, FileAccess.Read))
                                {
                                    artBoxPictureBox.Image = Image.FromStream(stream);
                                }
                            }
                            else
                            {
                                Logger.LogToFile($"Loading image into PictureBox from: {destPath}");
                                using (var stream = new FileStream(destPath, FileMode.Open, FileAccess.Read))
                                {
                                    artBoxPictureBox.Image = Image.FromStream(stream);
                                }
                            }

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
                        }
                    }
                    else
                    {
                        Logger.LogToFile($"{assetType} selection cancelled or no image selected.");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogToFile($"Failed to fetch {assetType.ToLower()}: {ex.Message}");
                MessageBox.Show($"Failed to fetch {assetType.ToLower()}: {ex.Message}");
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
                    string sanitizedDisplayName = game.DisplayName;
                    foreach (char invalidChar in Path.GetInvalidFileNameChars())
                    {
                        sanitizedDisplayName = sanitizedDisplayName.Replace(invalidChar, '_');
                    }
                    string uniqueFolderName = $"{sanitizedDisplayName}_{gameIds[game]}";
                    var gameDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ArcadeLauncher", "Assets", uniqueFolderName);
                    Logger.LogToFile($"Creating directory for game assets: {gameDir}");
                    Directory.CreateDirectory(gameDir);

                    // Dispose of the current image in the PictureBox to release file handles
                    if (pictureBox.Image != null)
                    {
                        Logger.LogToFile("Disposing of current PictureBox image to release file handles.");
                        pictureBox.Image.Dispose();
                        pictureBox.Image = null;
                        // Force garbage collection to ensure file handles are released
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                    }

                    if (assetType == "SplashScreen")
                    {
                        var destPath4k = Path.Combine(gameDir, "SplashScreen_4k.png");
                        var destPath1440p = Path.Combine(gameDir, "SplashScreen_1440p.png");
                        var destPath1080p = Path.Combine(gameDir, "SplashScreen_1080p.png");

                        using (var sourceImage = new Bitmap(dialog.FileName))
                        {
                            Logger.LogToFile($"Source image dimensions: {sourceImage.Width}x{sourceImage.Height}");

                            // 4K (3840x2160) - Always generate, even if source is smaller
                            using (var resizedImage = new Bitmap(3840, 2160))
                            using (var graphics4k = Graphics.FromImage(resizedImage))
                            {
                                graphics4k.InterpolationMode = InterpolationMode.HighQualityBicubic;
                                graphics4k.DrawImage(sourceImage, 0, 0, 3840, 2160);
                                resizedImage.Save(destPath4k, System.Drawing.Imaging.ImageFormat.Png);
                                Logger.LogToFile($"Saved 4K splash screen image to: {destPath4k} with dimensions: {resizedImage.Width}x{resizedImage.Height}");
                            }
                            game.SplashScreenPath["4k"] = destPath4k;
                            Logger.LogToFile($"Saved 4K splash screen image to: {destPath4k}");
                            if (sourceImage.Width < 3840 || sourceImage.Height < 2160)
                            {
                                Logger.LogToFile($"Warning: 4K variant upscaled from {sourceImage.Width}x{sourceImage.Height}, quality may be suboptimal.");
                            }

                            // 1440p (2560x1440)
                            using (var resizedImage = new Bitmap(2560, 1440))
                            using (var graphics1440p = Graphics.FromImage(resizedImage))
                            {
                                graphics1440p.InterpolationMode = InterpolationMode.HighQualityBicubic;
                                graphics1440p.DrawImage(sourceImage, 0, 0, 2560, 1440);
                                resizedImage.Save(destPath1440p, System.Drawing.Imaging.ImageFormat.Png);
                                Logger.LogToFile($"Saved 1440p splash screen image to: {destPath1440p} with dimensions: {resizedImage.Width}x{resizedImage.Height}");
                            }
                            game.SplashScreenPath["1440p"] = destPath1440p;
                            Logger.LogToFile($"Saved 1440p splash screen image to: {destPath1440p}");

                            // 1080p (1920x1080)
                            using (var resizedImage = new Bitmap(1920, 1080))
                            using (var graphics1080p = Graphics.FromImage(resizedImage))
                            {
                                graphics1080p.InterpolationMode = InterpolationMode.HighQualityBicubic;
                                graphics1080p.DrawImage(sourceImage, 0, 0, 1920, 1080);
                                resizedImage.Save(destPath1080p, System.Drawing.Imaging.ImageFormat.Png);
                                Logger.LogToFile($"Saved 1080p splash screen image to: {destPath1080p} with dimensions: {resizedImage.Width}x{resizedImage.Height}");
                            }
                            game.SplashScreenPath["1080p"] = destPath1080p;
                            Logger.LogToFile($"Saved 1080p splash screen image to: {destPath1080p}");
                        }

                        string displayPath = game.SplashScreenPath.ContainsKey("1440p") ? game.SplashScreenPath["1440p"] : game.SplashScreenPath["1080p"];
                        Logger.LogToFile($"Loading splash screen image into PictureBox from: {displayPath}");
                        using (var stream = new FileStream(displayPath, FileMode.Open, FileAccess.Read))
                        {
                            pictureBox.Image = Image.FromStream(stream);
                        }
                    }
                    else
                    {
                        var destPath = Path.Combine(gameDir, $"{assetType}.png");
                        var previewPath = Path.Combine(gameDir, $"{assetType}Preview.png");
                        Logger.LogToFile($"Destination path for {assetType}: {destPath}, Preview path: {previewPath}");

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
                                if (i == 4)
                                {
                                    Logger.LogToFile($"Failed to copy image after multiple attempts: {ex.Message}");
                                    MessageBox.Show($"Failed to copy image after multiple attempts: {ex.Message}");
                                    return;
                                }
                                Logger.LogToFile($"Copy attempt {i + 1} failed: {ex.Message}. Retrying after 500ms...");
                                System.Threading.Thread.Sleep(500);
                            }
                        }

                        if (assetType == "Marquee")
                        {
                            using (var tempImage = Image.FromFile(destPath))
                            using (var sourceImage = new Bitmap(tempImage))
                            {
                                Rectangle cropRect = new Rectangle(0, 0, 1920, 360);
                                using (var croppedBitmap = new Bitmap(1920, 360))
                                using (var graphicsPreview = Graphics.FromImage(croppedBitmap))
                                {
                                    graphicsPreview.InterpolationMode = InterpolationMode.HighQualityBicubic;
                                    graphicsPreview.DrawImage(sourceImage, new Rectangle(0, 0, 1920, 360), cropRect, GraphicsUnit.Pixel);
                                    croppedBitmap.Save(previewPath, System.Drawing.Imaging.ImageFormat.Png);
                                    Logger.LogToFile($"Saved cropped preview image to: {previewPath} with dimensions: {croppedBitmap.Width}x{croppedBitmap.Height}");
                                }
                            }
                            Logger.LogToFile($"Loading preview image into PictureBox from: {previewPath}");
                            using (var stream = new FileStream(previewPath, FileMode.Open, FileAccess.Read))
                            {
                                pictureBox.Image = Image.FromStream(stream);
                            }
                        }
                        else
                        {
                            Logger.LogToFile($"Loading image into PictureBox from: {destPath}");
                            using (var stream = new FileStream(destPath, FileMode.Open, FileAccess.Read))
                            {
                                pictureBox.Image = Image.FromStream(stream);
                            }
                        }

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
                    }
                }
            }
        }

        // Class to represent IGDB game data
        public class IGDBGame
        {
            public string Name { get; set; }
            public IGDBCover Cover { get; set; }
            public List<IGDBScreenshot> Screenshots { get; set; }
            public List<IGDBScreenshot> Artworks { get; set; }
        }

        public class IGDBCover
        {
            public string Url { get; set; }
        }

        public class IGDBScreenshot
        {
            public string Url { get; set; }
        }

        // Form to display cover art or screenshot options
        public class CoverArtSelectionForm : Form
        {
            private List<IGDBGame> games;
            private List<PictureBox> pictureBoxes;
            private HttpClient httpClient;
            private FlowLayoutPanel flowLayoutPanel;
            private string assetType;
            public string SelectedCoverUrl { get; private set; }

            public CoverArtSelectionForm(List<IGDBGame> games, HttpClient httpClient, string assetType = "ArtBox")
            {
                this.games = games;
                this.httpClient = httpClient;
                this.assetType = assetType;

                // Enable double-buffering to reduce flickering
                DoubleBuffered = true;

                double scalingFactor = (double)Screen.PrimaryScreen.WorkingArea.Height / 1080;
                Logger.LogToFile($"CoverArtSelectionForm Scaling Factor: scalingFactor={scalingFactor}, ScreenHeight={Screen.PrimaryScreen.WorkingArea.Height}");

                int baseThumbnailWidth = assetType == "SplashScreen" ? 333 : 198; // Smaller thumbnails for CoverArt to fit 5 columns in same width as 3 SplashScreen columns
                int thumbnailWidth = (int)(baseThumbnailWidth * scalingFactor);
                int thumbnailHeight = (int)(thumbnailWidth * (assetType == "SplashScreen" ? 9.0 / 16.0 : 4.0 / 3.0));
                int gap = (int)(5 * scalingFactor);
                int columns = assetType == "SplashScreen" ? 3 : 5; // 3 columns for SplashScreen, 5 for CoverArt
                int margin = (int)(18 * scalingFactor);
                int scrollbarWidth = (int)(20 * scalingFactor);
                int formWidth = (columns * thumbnailWidth) + ((columns - 1) * gap) + (2 * margin) + scrollbarWidth + (int)(10 * scalingFactor) + 25;
                int formHeight = (int)(500 * scalingFactor);

                this.Size = new Size(formWidth, formHeight);
                this.StartPosition = FormStartPosition.CenterScreen;
                this.BackColor = ColorTranslator.FromHtml("#F3F3F3");
                this.FormBorderStyle = FormBorderStyle.FixedSingle;
                this.ControlBox = true;
                this.MinimizeBox = false;
                this.MaximizeBox = false;
                this.ShowIcon = false;
                this.Text = assetType == "SplashScreen" ? "Select Splash Screen Image" : "Select Cover Art";
                pictureBoxes = new List<PictureBox>();

                flowLayoutPanel = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    AutoScroll = true,
                    Padding = new Padding(margin, margin, margin, margin)
                };
                this.Controls.Add(flowLayoutPanel);
            }

            public async Task LoadAndShowAsync()
            {
                double scalingFactor = (double)Screen.PrimaryScreen.WorkingArea.Height / 1080;
                int baseThumbnailWidth = assetType == "SplashScreen" ? 333 : 198;
                int thumbnailWidth = (int)(baseThumbnailWidth * scalingFactor);
                int gap = (int)(5 * scalingFactor);

                // Load images while the form is hidden
                await LoadCoverArtImagesAsync(thumbnailWidth, gap);

                // Show the form as a modal dialog after images are loaded
                ShowDialog();
            }

            private async Task LoadCoverArtImagesAsync(int thumbnailWidth, int gap)
            {
                Logger.LogToFile($"Loading images for {games.Count} games (Asset Type: {assetType}).");
                IEnumerable<IGDBGame> filteredGames;
                if (assetType == "SplashScreen")
                {
                    filteredGames = games.Where(g => (g.Artworks != null && g.Artworks.Any(a => !string.IsNullOrEmpty(a.Url))) || (g.Screenshots != null && g.Screenshots.Any(s => !string.IsNullOrEmpty(s.Url))));
                }
                else
                {
                    filteredGames = games.Where(g => g.Cover != null && !string.IsNullOrEmpty(g.Cover.Url));
                }

                var imageTasks = new List<Task<(IGDBGame game, string coverUrl, byte[] imageBytes, string imageType)>>();
                int totalImages = 0;
                const int maxImages = 60;

                // First, load artworks for SplashScreen
                if (assetType == "SplashScreen")
                {
                    foreach (var game in filteredGames)
                    {
                        if (game.Artworks != null)
                        {
                            foreach (var artwork in game.Artworks)
                            {
                                if (totalImages >= maxImages) break;
                                if (!string.IsNullOrEmpty(artwork.Url))
                                {
                                    imageTasks.Add(FetchImageAsync(game, artwork.Url, "t_screenshot_big", "artwork"));
                                    totalImages++;
                                }
                            }
                        }
                        if (totalImages >= maxImages) break;
                    }

                    // If we haven't reached the limit, fill with screenshots
                    if (totalImages < maxImages)
                    {
                        foreach (var game in filteredGames)
                        {
                            if (game.Screenshots != null)
                            {
                                foreach (var screenshot in game.Screenshots)
                                {
                                    if (totalImages >= maxImages) break;
                                    if (!string.IsNullOrEmpty(screenshot.Url))
                                    {
                                        imageTasks.Add(FetchImageAsync(game, screenshot.Url, "t_screenshot_big", "screenshot"));
                                        totalImages++;
                                    }
                                }
                            }
                            if (totalImages >= maxImages) break;
                        }
                    }
                }
                else
                {
                    // For CoverArt, load covers as before
                    foreach (var game in filteredGames)
                    {
                        imageTasks.Add(FetchImageAsync(game, game.Cover.Url, "t_cover_big", "cover"));
                    }
                }

                // Process images in batches of 20 to avoid overwhelming the network
                const int batchSize = 20;

                // Suspend layout to prevent incremental rendering
                flowLayoutPanel.SuspendLayout();

                for (int i = 0; i < imageTasks.Count; i += batchSize)
                {
                    var batch = imageTasks.Skip(i).Take(batchSize).ToList();
                    var results = await Task.WhenAll(batch);

                    foreach (var (game, coverUrl, imageBytes, imageType) in results)
                    {
                        if (imageBytes == null) continue; // Skip failed downloads

                        try
                        {
                            using (var ms = new MemoryStream(imageBytes))
                            using (var tempImage = Image.FromStream(ms))
                            {
                                Logger.LogToFile($"Thumbnail image dimensions: {tempImage.Width}x{tempImage.Height}");
                                if (tempImage.Width < 1920 || tempImage.Height < 1080)
                                {
                                    Logger.LogToFile($"Warning: Thumbnail image is lower resolution than expected (1920x1080).");
                                }

                                var pictureBox = new PictureBox
                                {
                                    Size = new Size(thumbnailWidth, (int)(thumbnailWidth * (assetType == "SplashScreen" ? 9.0 / 16.0 : 4.0 / 3.0))),
                                    SizeMode = PictureBoxSizeMode.Zoom,
                                    Image = new Bitmap(tempImage), // Set the image immediately
                                    BorderStyle = BorderStyle.FixedSingle,
                                    BackColor = Color.Black,
                                    Margin = new Padding(gap / 2)
                                };

                                pictureBox.Paint += (s, e) =>
                                {
                                    e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                                    e.Graphics.DrawImage(pictureBox.Image, pictureBox.ClientRectangle);
                                };

                                pictureBox.Click += (s, e) =>
                                {
                                    SelectedCoverUrl = coverUrl;
                                    Logger.LogToFile($"Selected {(assetType == "SplashScreen" ? "splash screen" : "cover art")} image for {game.Name}: {SelectedCoverUrl}");
                                    this.Close();
                                };

                                pictureBoxes.Add(pictureBox);
                                flowLayoutPanel.Controls.Add(pictureBox);
                                Logger.LogToFile($"Added picture box for {game.Name} ({imageType})");
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogToFile($"Failed to load {(assetType == "SplashScreen" ? "splash screen" : "cover art")} thumbnail for {game.Name}: {ex.Message}");
                        }
                    }
                }

                // Resume layout after all controls are added
                flowLayoutPanel.ResumeLayout(true);

                Logger.LogToFile($"Finished loading images. Total images loaded: {pictureBoxes.Count}");
                if (pictureBoxes.Count == 0)
                {
                    Logger.LogToFile("No images were loaded. Closing form.");
                    MessageBox.Show("No images could be loaded.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    this.Close();
                }
            }

            private async Task<(IGDBGame game, string coverUrl, byte[] imageBytes, string imageType)> FetchImageAsync(IGDBGame game, string coverUrl, string size, string imageType)
            {
                try
                {
                    Logger.LogToFile($"Processing game: {game.Name}, {(imageType == "artwork" ? "Artwork" : imageType == "screenshot" ? "Screenshot" : "Cover")} URL: {coverUrl}");
                    var highResUrl = coverUrl.Replace("t_thumb", size);
                    Logger.LogToFile($"Fetching thumbnail image from: {highResUrl}");
                    var imageBytes = await httpClient.GetByteArrayAsync($"https:{highResUrl}");
                    Logger.LogToFile($"Successfully fetched thumbnail image for {game.Name}, size: {imageBytes.Length} bytes");
                    return (game, coverUrl, imageBytes, imageType);
                }
                catch (Exception ex)
                {
                    Logger.LogToFile($"Failed to load {(assetType == "SplashScreen" ? "splash screen" : "cover art")} thumbnail for {game.Name}: {ex.Message}");
                    return (game, coverUrl, null, imageType);
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