using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using ArcadeLauncher.Core;
using ArcadeLauncher.Plugins;

namespace ArcadeLauncher.SW2
{
    public partial class MainForm : Form
    {
        private List<Game> games;
        private Settings settings;
        private List<IEmulatorPlugin> plugins;
        private HttpClient httpClient;
        private ListBox gameList;
        private Panel mainPanel;
        private EventHandler selectedIndexChangedHandler;
        private ToolStripButton deleteGameButton;
        private ToolStrip toolStrip; // Added to access the Top Ribbon height
        private string twitchClientId;
        private string twitchClientSecret;
        private string twitchAccessToken;
        private DateTime tokenExpiration;
        private Dictionary<Game, string> gameIds = new Dictionary<Game, string>(); // Map Game instances to unique IDs
        private int preLaunchFirstInputTop; // Added class-level field
        private int postCloseFirstInputTop; // Added class-level field
        private Font largeFont;

        // Game Tab UI fields (moved from SetupGameView to be shared across partial classes)
        private List<TextBox> preLaunchCommandsList;
        private List<Button> preLaunchMinusButtons;
        private List<Button> preLaunchPlusButtons;
        private List<TextBox> postCloseCommandsList;
        private List<Button> postCloseMinusButtons;
        private List<Button> postClosePlusButtons;
        private Label postCloseLabel;
        private Button saveButton;
        private Button cancelButton;
        private int inputHeight;
        private int inputWidth;
        private int column2Left;
        private int column3Left;
        private int smallButtonWidth;
        private int smallButtonHeight;
        private int smallButtonGap;
        private TextBox notesTextBox; // Already added for Notes Section
        private Label notesLabel; // Already added for Notes label

        // Custom ListBox class to enable double-buffering (Ticket 04)
        private class CustomListBox : ListBox
        {
            public CustomListBox()
            {
                DoubleBuffered = true;
            }
        }

        // Custom Panel class to enable double-buffering (Ticket 04)
        private class CustomPanel : Panel
        {
            public CustomPanel()
            {
                DoubleBuffered = true;
            }
        }

        // Custom PictureBox class to enable double-buffering (Ticket 04)
        private class CustomPictureBox : PictureBox
        {
            public CustomPictureBox()
            {
                DoubleBuffered = true;
            }
        }

        // Custom TextBox class to enable double-buffering (Ticket 04)
        private class CustomTextBox : TextBox
        {
            public CustomTextBox()
            {
                DoubleBuffered = true;
            }
        }

        // Custom ComboBox class to enable double-buffering (Ticket 04)
        private class CustomComboBox : ComboBox
        {
            public CustomComboBox()
            {
                DoubleBuffered = true;
            }
        }

        // Custom Button class to enable double-buffering (Ticket 04)
        private class CustomButton : Button
        {
            public CustomButton()
            {
                DoubleBuffered = true;
            }
        }

        public MainForm()
        {
            InitializeComponent();
            DoubleBuffered = true; // Enable double-buffering to reduce flickering (Ticket 04)

            // Load Twitch API keys from .env file
            LoadTwitchApiKeys();

            // Calculate the Windows scaling factor (DPI scaling)
            float windowsScalingFactor;
            using (Graphics g = this.CreateGraphics())
            {
                windowsScalingFactor = g.DpiX / 96f; // Default DPI at 100% scaling is 96
            }
            Logger.LogToFile($"Windows Scaling Factor: {windowsScalingFactor} (DPI: {windowsScalingFactor * 96})");

            // Adjust the font size to counteract Windows scaling
            float baseFontSize = 15f; // Desired font size at 100% scaling (previously 12 * 1.25 = 15)
            float adjustedFontSize = baseFontSize / windowsScalingFactor;
            largeFont = new Font("Microsoft Sans Serif", adjustedFontSize);
            Logger.LogToFile($"Adjusted Font Size: Base={baseFontSize}, Adjusted={adjustedFontSize}");

            LoadData();
            SetupUI();
            httpClient = new HttpClient();
            // Initialize Twitch access token (Ticket 02 - Remove notification)
            try
            {
                Task.Run(() => RefreshTwitchTokenAsync()).Wait();
            }
            catch (Exception)
            {
                // Silently fail - notification will be shown on search if needed
            }
        }

        private void LoadTwitchApiKeys()
        {
            try
            {
                // Read the .env file
                string envPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".env");
                if (!File.Exists(envPath))
                {
                    throw new FileNotFoundException("The .env file was not found. Please create a .env file with TWITCH_CLIENT_ID and TWITCH_CLIENT_SECRET.");
                }

                var envLines = File.ReadAllLines(envPath);
                foreach (var line in envLines)
                {
                    // Skip empty lines or comments
                    if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"))
                        continue;

                    // Split the line into key and value, taking only the first '='
                    var parts = line.Split(new char[] { '=' }, 2, StringSplitOptions.None); // Use the correct overload
                    if (parts.Length != 2)
                        continue;

                    var key = parts[0].Trim();
                    var value = parts[1].Trim();

                    if (key == "TWITCH_CLIENT_ID")
                    {
                        twitchClientId = value;
                    }
                    else if (key == "TWITCH_CLIENT_SECRET")
                    {
                        twitchClientSecret = value;
                    }
                }

                // Validate that both keys were loaded
                if (string.IsNullOrEmpty(twitchClientId) || string.IsNullOrEmpty(twitchClientSecret))
                {
                    throw new InvalidOperationException("TWITCH_CLIENT_ID or TWITCH_CLIENT_SECRET not found in .env file.");
                }

                Logger.LogToFile("Successfully loaded Twitch API keys from .env file.");
            }
            catch (Exception ex)
            {
                Logger.LogToFile($"Failed to load Twitch API keys from .env file: {ex.Message}");
                throw;
            }
        }

        private async Task RefreshTwitchTokenAsync()
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, "https://id.twitch.tv/oauth2/token");
                var parameters = new Dictionary<string, string>
                {
                    { "client_id", twitchClientId },
                    { "client_secret", twitchClientSecret },
                    { "grant_type", "client_credentials" }
                };
                request.Content = new FormUrlEncodedContent(parameters); // Fixed typo: added dot between request and Content
                var response = await httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                var jsonResponse = await response.Content.ReadAsStringAsync();
                var tokenData = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonResponse);
                twitchAccessToken = tokenData["access_token"].ToString();
                int expiresIn = int.Parse(tokenData["expires_in"].ToString());
                tokenExpiration = DateTime.UtcNow.AddSeconds(expiresIn - 300); // Refresh 5 minutes before expiration
                httpClient.DefaultRequestHeaders.Remove("Client-ID");
                httpClient.DefaultRequestHeaders.Remove("Authorization");
                httpClient.DefaultRequestHeaders.Add("Client-ID", twitchClientId);
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {twitchAccessToken}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to refresh Twitch token: {ex.Message}", ex);
            }
        }

        private async Task EnsureValidTwitchTokenAsync()
        {
            if (DateTime.UtcNow >= tokenExpiration)
            {
                await RefreshTwitchTokenAsync();
            }
        }

        private void LoadData()
        {
            games = DataManager.LoadGameData().Games.OrderBy(g => g.AlphabetizeName).ToList();
            // Populate gameIds for existing games loaded from JSON
            foreach (var game in games)
            {
                if (!gameIds.ContainsKey(game))
                {
                    gameIds[game] = Guid.NewGuid().ToString();
                    Logger.LogToFile($"Assigned new ID to existing game: DisplayName={game.DisplayName}, ID={gameIds[game]}");
                }
            }
            settings = DataManager.LoadSettings();
            plugins = LoadPlugins();
        }

        private List<IEmulatorPlugin> LoadPlugins()
        {
            string exePath = AppDomain.CurrentDomain.BaseDirectory;
            string pluginDir = Path.Combine(exePath, "..", "Plugins");
            pluginDir = Path.GetFullPath(pluginDir);
            var pluginList = new List<IEmulatorPlugin>();

            if (!Directory.Exists(pluginDir))
            {
                Logger.LogToFile($"Plugins directory not found: {pluginDir}");
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
                            Logger.LogToFile($"Loaded plugin: {dll}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogToFile($"Failed to load plugin from {dll}: {ex.Message}");
                }
            }
            return pluginList;
        }
    }
}