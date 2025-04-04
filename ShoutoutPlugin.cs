using System;
using System.IO;
using System.Timers;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Microsoft.Xna.Framework;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Generic;
using System.Reflection;
using System.Globalization;

namespace ShoutoutPlugin
{
    [ApiVersion(2, 1)]
    public class ShoutoutPlugin : TerrariaPlugin
    {
        private Timer _reminderTimer;
        private ShoutoutConfig _config;
        private ShoutoutStats _stats;
        private ShoutoutLogger _logger;
        private readonly ConcurrentDictionary<string, DateTime> _lastShoutoutTime;
        private readonly string _configPath = Path.Combine(TShock.SavePath, "ShoutoutConfig.json");
        private readonly string _statsPath = Path.Combine(TShock.SavePath, "ShoutoutStats.json");
        private readonly ConcurrentDictionary<string, int> _shoutoutCache;
        
        public override string Author => "Cascade";
        public override string Description => "A plugin that allows players to leave shoutouts with advanced features";
        public override string Name => "Shoutout Plugin";
        public override Version Version => new Version(1, 1, 0);

        public ShoutoutPlugin(Main game) : base(game)
        {
            _lastShoutoutTime = new ConcurrentDictionary<string, DateTime>();
            _shoutoutCache = new ConcurrentDictionary<string, int>();
        }

        public override void Initialize()
        {
            TShock.Log.Info("[Shoutout] Plugin initialization starting");
            
            // Load configuration
            _config = ShoutoutConfig.Load(_configPath);
            TShock.Log.Info($"[Shoutout] Configuration loaded from: {_configPath}");
            
            _stats = ShoutoutStats.Load(_statsPath);
            TShock.Log.Info("[Shoutout] Statistics loaded");
            
            _logger = new ShoutoutLogger(_config);
            TShock.Log.Info("[Shoutout] Logger initialized");

            // Initialize timer with configured interval
            _reminderTimer = new Timer(_config.ReminderIntervalMinutes * 60 * 1000);
            _reminderTimer.Elapsed += OnReminderTimerElapsed;
            _reminderTimer.Start();
            TShock.Log.Info("[Shoutout] Reminder timer initialized");

            // Register commands with security checks
            Commands.ChatCommands.Add(new Command("shoutout.use", HandleShoutout, "shoutout")
            {
                HelpText = "Leave a shoutout message that will be logged. Usage: /shoutout <message>"
            });

            Commands.ChatCommands.Add(new Command("shoutout.admin", HandleShoutoutAdmin, "shoutoutadmin")
            {
                HelpText = "Admin commands for the shoutout system. Usage: /shoutoutadmin <stats|config|clear>"
            });

            Commands.ChatCommands.Add(new Command("shoutout.view", HandleViewShoutouts, "shoutouts")
            {
                HelpText = "View the last 10 shoutouts. Usage: /shoutouts [number]"
            });

            TShock.Log.Info("[Shoutout] Commands registered");
            TShock.Log.Info("[Shoutout] Plugin initialization complete");

            // Security: Add hooks for player leave to clean up resources
            ServerApi.Hooks.ServerLeave.Register(this, OnPlayerLeave);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _reminderTimer?.Dispose();
                SaveData();
            }
            base.Dispose(disposing);
        }

        private void SaveData()
        {
            try
            {
                _config.Save(_configPath);
                _stats.Save(_statsPath);
            }
            catch (Exception ex)
            {
                TShock.Log.Error($"Error saving shoutout data: {ex.Message}");
            }
        }

        private async void HandleShoutout(CommandArgs args)
        {
            TShock.Log.Info("[Shoutout] Starting to process shoutout command");
            
            if (args.Parameters.Count == 0)
            {
                args.Player.SendErrorMessage("Usage: /shoutout <message>");
                return;
            }

            // Security: Rate limiting
            if (!CanPlayerShout(args.Player))
            {
                TimeSpan waitTime = GetWaitTime(args.Player);
                args.Player.SendErrorMessage($"Please wait {waitTime.TotalSeconds:F0} seconds before sending another shoutout.");
                return;
            }

            string message = string.Join(" ", args.Parameters);
            TShock.Log.Info($"[Shoutout] Processing message from {args.Player.Name}: {message}");

            // Security: Input validation
            if (message.Length > _config.MaxMessageLength)
            {
                args.Player.SendErrorMessage($"Message too long! Maximum length is {_config.MaxMessageLength} characters.");
                return;
            }

            // Security: Sanitize input
            message = SanitizeInput(message);

            try
            {
                DateTime now = DateTime.Now;
                
                // Log to file
                await _logger.LogShoutoutAsync(args.Player.Name, message, now);
                TShock.Log.Info("[Shoutout] Message logged to file");
                
                // Update statistics
                _stats.UpdatePlayerStats(args.Player.Name, now);
                UpdatePlayerShoutoutTime(args.Player);
                TShock.Log.Info("[Shoutout] Statistics updated");

                // Cache the shoutout
                _shoutoutCache.AddOrUpdate(GenerateShoutoutKey(args.Player.Name, message), 1, (_, __) => 1);
                TShock.Log.Info("[Shoutout] Message cached");

                // Send in-game message
                Color messageColor = GetColorFromString(_config.MessageColor);
                if (_config.EnableSounds)
                {
                    args.Player.SendData((PacketTypes)45, "", 25);
                }

                TSPlayer.All.SendMessage($"{args.Player.Name}: {message}", messageColor);
                args.Player.SendSuccessMessage("Your shoutout has been logged!");
                TShock.Log.Info("[Shoutout] Process completed successfully");
            }
            catch (Exception ex)
            {
                TShock.Log.Error($"[Shoutout] Error processing shoutout: {ex.Message}");
                TShock.Log.Error($"[Shoutout] Stack trace: {ex.StackTrace}");
                args.Player.SendErrorMessage("There was an error processing your shoutout.");
            }
        }

        private void HandleShoutoutAdmin(CommandArgs args)
        {
            if (args.Parameters.Count == 0)
            {
                args.Player.SendErrorMessage("Usage: /shoutoutadmin <stats|config|clear>");
                return;
            }

            switch (args.Parameters[0].ToLower())
            {
                case "stats":
                    ShowStats(args.Player);
                    break;
                case "config":
                    if (args.Parameters.Count == 1)
                    {
                        // Show current configuration
                        args.Player.SendInfoMessage("=== Current Configuration ===");
                        args.Player.SendInfoMessage($"CooldownSeconds: {_config.CooldownSeconds}");
                        args.Player.SendInfoMessage($"MaxMessageLength: {_config.MaxMessageLength}");
                        args.Player.SendInfoMessage($"ReminderIntervalMinutes: {_config.ReminderIntervalMinutes}");
                        return;
                    }
                    HandleConfig(args);
                    break;
                case "clear":
                    ClearShoutouts(args.Player);
                    break;
                default:
                    args.Player.SendErrorMessage("Invalid command. Use stats, config, or clear.");
                    break;
            }
        }

        private void ShowStats(TSPlayer player)
        {
            var topUsers = _stats.GetTopUsers();
            var peakHours = _stats.GetPeakHours();

            player.SendInfoMessage("=== Shoutout Statistics ===");
            player.SendInfoMessage($"Total Shoutouts: {_stats.TotalShoutouts}");
            player.SendInfoMessage("Top Users:");
            foreach (var user in topUsers)
            {
                player.SendInfoMessage($"- {user.Key}: {user.Value}");
            }
            player.SendInfoMessage("Peak Hours:");
            foreach (var hour in peakHours)
            {
                player.SendInfoMessage($"- {hour.Hours:D2}:00");
            }
        }

        private void HandleConfig(CommandArgs args)
        {
            if (args.Parameters.Count < 2)
            {
                args.Player.SendErrorMessage("Usage: /shoutoutadmin config <setting> <value>");
                return;
            }

            string setting = args.Parameters[1];
            string value = args.Parameters.Count > 2 ? string.Join(" ", args.Parameters.Skip(2)) : null;

            if (string.IsNullOrEmpty(value))
            {
                args.Player.SendErrorMessage("Please provide a value for the setting.");
                return;
            }

            try
            {
                var property = typeof(ShoutoutConfig).GetProperty(setting, BindingFlags.Public | BindingFlags.Instance);
                if (property == null)
                {
                    args.Player.SendErrorMessage($"Invalid setting: {setting}");
                    return;
                }

                object convertedValue;
                try
                {
                    if (property.PropertyType == typeof(bool))
                    {
                        convertedValue = bool.Parse(value);
                    }
                    else if (property.PropertyType == typeof(int))
                    {
                        convertedValue = int.Parse(value, CultureInfo.InvariantCulture);
                    }
                    else if (property.PropertyType == typeof(string))
                    {
                        convertedValue = value;
                    }
                    else
                    {
                        args.Player.SendErrorMessage($"Unsupported setting type: {property.PropertyType.Name}");
                        return;
                    }
                }
                catch (Exception)
                {
                    args.Player.SendErrorMessage($"Invalid value format for {setting}. Expected type: {property.PropertyType.Name}");
                    return;
                }

                // Validate specific settings
                if (setting.Equals("CacheSize", StringComparison.OrdinalIgnoreCase))
                {
                    int cacheSize = (int)convertedValue;
                    if (cacheSize < 1 || cacheSize > 1000)
                    {
                        args.Player.SendErrorMessage("Cache size must be between 1 and 1000.");
                        return;
                    }
                }
                else if (setting.Equals("MaxMessageLength", StringComparison.OrdinalIgnoreCase))
                {
                    int maxLength = (int)convertedValue;
                    if (maxLength < 1 || maxLength > 1000)
                    {
                        args.Player.SendErrorMessage("Message length must be between 1 and 1000.");
                        return;
                    }
                }
                else if (setting.Equals("CooldownSeconds", StringComparison.OrdinalIgnoreCase))
                {
                    int cooldown = (int)convertedValue;
                    if (cooldown < 0)
                    {
                        args.Player.SendErrorMessage("Cooldown cannot be negative.");
                        return;
                    }
                }

                // Set the value
                property.SetValue(_config, convertedValue);
                _config.Save(_configPath);

                args.Player.SendSuccessMessage($"Successfully updated {setting} to {value}");

                // Apply immediate changes if needed
                if (setting.Equals("ReminderIntervalMinutes", StringComparison.OrdinalIgnoreCase))
                {
                    _reminderTimer.Interval = (int)convertedValue * 60 * 1000;
                }
            }
            catch (Exception ex)
            {
                TShock.Log.Error($"Error updating config: {ex.Message}");
                args.Player.SendErrorMessage("An error occurred while updating the configuration.");
            }
        }

        private void ClearShoutouts(TSPlayer player)
        {
            _shoutoutCache.Clear();
            player.SendSuccessMessage("Shoutout cache cleared.");
        }

        private void OnPlayerLeave(LeaveEventArgs args)
        {
            string playerName = TShock.Players[args.Who]?.Name;
            if (playerName != null)
            {
                _lastShoutoutTime.TryRemove(playerName, out _);
            }
        }

        private void OnReminderTimerElapsed(object sender, ElapsedEventArgs e)
        {
            TSPlayer.All.SendMessage(_config.ReminderMessage, GetColorFromString(_config.MessageColor));
        }

        private bool CanPlayerShout(TSPlayer player)
        {
            if (!_lastShoutoutTime.TryGetValue(player.Name, out DateTime lastTime))
            {
                return true;
            }

            ShoutoutStats.PlayerStats stats = null;
            _stats.PlayerStatistics.TryGetValue(player.Name, out stats);
            if (stats != null && stats.ShoutoutsToday >= _config.MaxShoutoutsPerDay)
            {
                return false;
            }

            return (DateTime.Now - lastTime).TotalSeconds >= _config.CooldownSeconds;
        }

        private TimeSpan GetWaitTime(TSPlayer player)
        {
            if (_lastShoutoutTime.TryGetValue(player.Name, out DateTime lastTime))
            {
                var timeSinceLastShoutout = DateTime.Now - lastTime;
                var cooldownDuration = TimeSpan.FromSeconds(_config.CooldownSeconds);
                if (timeSinceLastShoutout < cooldownDuration)
                {
                    return cooldownDuration - timeSinceLastShoutout;
                }
            }
            return TimeSpan.Zero;
        }

        private void UpdatePlayerShoutoutTime(TSPlayer player)
        {
            _lastShoutoutTime.AddOrUpdate(player.Name, DateTime.Now, (_, __) => DateTime.Now);
        }

        private string SanitizeInput(string input)
        {
            // Security: Remove potentially dangerous characters and limit length
            return new string(input.Where(c => !char.IsControl(c)).Take(_config.MaxMessageLength).ToArray());
        }

        private Color GetColorFromString(string colorName)
        {
            try
            {
                var prop = typeof(Color).GetProperty(colorName);
                return prop != null ? (Color)prop.GetValue(null) : Color.Yellow;
            }
            catch
            {
                return Color.Yellow;
            }
        }

        private string GenerateShoutoutKey(string playerName, string message)
        {
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes($"{playerName}:{message}:{DateTime.Now.Ticks}"));
                return Convert.ToBase64String(hash);
            }
        }

        private void HandleViewShoutouts(CommandArgs args)
        {
            int count = 10; // Default number of shoutouts to show

            if (args.Parameters.Count > 0)
            {
                if (!int.TryParse(args.Parameters[0], out count))
                {
                    args.Player.SendErrorMessage("Invalid number format. Usage: /shoutouts [number]");
                    return;
                }

                if (count < 1 || count > 50)
                {
                    args.Player.SendErrorMessage("Number of shoutouts must be between 1 and 50.");
                    return;
                }
            }

            try
            {
                var recentShoutouts = GetRecentShoutouts(count);
                if (recentShoutouts.Count == 0)
                {
                    args.Player.SendInfoMessage("No shoutouts found.");
                    return;
                }

                args.Player.SendInfoMessage($"=== Last {recentShoutouts.Count} Shoutout(s) ===");
                foreach (var shoutout in recentShoutouts)
                {
                    args.Player.SendMessage(shoutout, GetColorFromString(_config.MessageColor));
                }
            }
            catch (Exception ex)
            {
                TShock.Log.Error($"Error reading shoutouts: {ex.Message}");
                args.Player.SendErrorMessage("An error occurred while reading shoutouts.");
            }
        }

        private List<string> GetRecentShoutouts(int count)
        {
            var shoutouts = new List<string>();
            
            try
            {
                if (!File.Exists(_config.LogFilePath))
                {
                    return shoutouts;
                }

                // Read all lines and take the last 'count' lines
                var allLines = File.ReadAllLines(_config.LogFilePath);
                var recentLines = allLines.Reverse().Take(count).Reverse();

                foreach (var line in recentLines)
                {
                    // Handle different log formats
                    if (_config.LogFormat.Equals("json", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            var entry = Newtonsoft.Json.JsonConvert.DeserializeObject<ShoutoutEntry>(line);
                            shoutouts.Add($"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss}] {entry.PlayerName}: {entry.Message}");
                        }
                        catch { continue; } // Skip invalid JSON entries
                    }
                    else if (_config.LogFormat.Equals("csv", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = line.Split(',');
                        if (parts.Length >= 3)
                        {
                            shoutouts.Add($"[{parts[0]}] {parts[1]}: {parts[2]}");
                        }
                    }
                    else // txt format
                    {
                        shoutouts.Add(line);
                    }
                }
            }
            catch (Exception ex)
            {
                TShock.Log.Error($"Error reading shoutout log: {ex.Message}");
            }

            return shoutouts;
        }
    }
}
