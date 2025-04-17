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
                            try
                            {
                                imageBytes = await httpClient.GetByteArrayAsync($"https:{highResUrl}");
                            }
                            catch (Exception ex)
                            {
                                Logger.LogToFile($"Failed to fetch t_1080p image: {ex.Message}");
                                highResUrl = selectedCoverUrl.Replace("t_thumb", "t_4k");
                                try
                                {
                                    imageBytes = await httpClient.GetByteArrayAsync($"https:{highResUrl}");
                                }
                                catch (Exception ex2)
                                {
                                    Logger.LogToFile($"Failed to fetch t_4k image: {ex2.Message}");
                                    highResUrl = selectedCoverUrl.Replace("t_thumb", "t_screenshot_big");
                                    try
                                    {
                                        imageBytes = await httpClient.GetByteArrayAsync($"https:{highResUrl}");
                                    }
                                    catch (Exception ex3)
                                    {
                                        Logger.LogToFile($"Failed to fetch t_screenshot_big image: {ex3.Message}");
                                        highResUrl = selectedCoverUrl;
                                        imageBytes = await httpClient.GetByteArrayAsync($"https:{highResUrl}");
                                    }
                                }
                            }
                        }
                        else
                        {
                            highResUrl = selectedCoverUrl.Replace("t_thumb", "t_1080p");
                            imageBytes = await httpClient.GetByteArrayAsync($"https:{highResUrl}");
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
                                    Logger.LogToFile($"Saved 4K splash screen image to: {destPath4k}");
                                }
                                game.SplashScreenPath["4k"] = destPath4k;
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
                                    Logger.LogToFile($"Saved 1440p splash screen image to: {destPath1440p}");
                                }
                                game.SplashScreenPath["1440p"] = destPath1440p;

                                // 1080p (1920x1080)
                                var destPath1080p = Path.Combine(gameDir, "SplashScreen_1080p.png");
                                using (var resizedImage = new Bitmap(1920, 1080))
                                using (var graphics1080p = Graphics.FromImage(resizedImage))
                                {
                                    graphics1080p.InterpolationMode = InterpolationMode.HighQualityBicubic;
                                    graphics1080p.DrawImage(sourceImage, 0, 0, 1920, 1080);
                                    resizedImage.Save(destPath1080p, System.Drawing.Imaging.ImageFormat.Png);
                                    Logger.LogToFile($"Saved 1080p splash screen image to: {destPath1080p}");
                                }
                                game.SplashScreenPath["1080p"] = destPath1080p;
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
                                        Logger.LogToFile($"Saved cropped preview image to: {previewPath}");
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
                            // 4K (3840x2160) - Always generate, even if source is smaller
                            using (var resizedImage = new Bitmap(3840, 2160))
                            using (var graphics4k = Graphics.FromImage(resizedImage))
                            {
                                graphics4k.InterpolationMode = InterpolationMode.HighQualityBicubic;
                                graphics4k.DrawImage(sourceImage, 0, 0, 3840, 2160);
                                resizedImage.Save(destPath4k, System.Drawing.Imaging.ImageFormat.Png);
                                Logger.LogToFile($"Saved 4K splash screen image to: {destPath4k}");
                            }
                            game.SplashScreenPath["4k"] = destPath4k;
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
                                Logger.LogToFile($"Saved 1440p splash screen image to: {destPath1440p}");
                            }
                            game.SplashScreenPath["1440p"] = destPath1440p;

                            // 1080p (1920x1080)
                            using (var resizedImage = new Bitmap(1920, 1080))
                            using (var graphics1080p = Graphics.FromImage(resizedImage))
                            {
                                graphics1080p.InterpolationMode = InterpolationMode.HighQualityBicubic;
                                graphics1080p.DrawImage(sourceImage, 0, 0, 1920, 1080);
                                resizedImage.Save(destPath1080p, System.Drawing.Imaging.ImageFormat.Png);
                                Logger.LogToFile($"Saved 1080p splash screen image to: {destPath1080p}");
                            }
                            game.SplashScreenPath["1080p"] = destPath1080p;
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
                                    Logger.LogToFile($"Saved cropped preview image to: {previewPath}");
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
                    var highResUrl = coverUrl.Replace("t_thumb", size);
                    var imageBytes = await httpClient.GetByteArrayAsync($"https:{highResUrl}");
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