using System;
using System.IO;
using Newtonsoft.Json;

namespace ShoutoutPlugin
{
    public class ShoutoutConfig
    {
        public int ReminderIntervalMinutes { get; set; } = 60;
        public string ReminderMessage { get; set; } = "Let us know you were here! /shoutout";
        public int MaxMessageLength { get; set; } = 200;
        public string LogFilePath { get; set; } = "shoutouts.log";
        public string LogFormat { get; set; } = "txt"; // txt, json, or csv
        public int LogRotationSizeMB { get; set; } = 10;
        public int MaxLogFiles { get; set; } = 5;
        public int CooldownSeconds { get; set; } = 60;
        public bool EnableSounds { get; set; } = true;
        public string MessageColor { get; set; } = "Yellow";
        public int MaxShoutoutsPerDay { get; set; } = 10;
        public bool RequireApproval { get; set; } = false;
        public bool EnableAnonymous { get; set; } = true;
        public int CacheSize { get; set; } = 100;

        public static ShoutoutConfig Load(string path)
        {
            if (File.Exists(path))
            {
                return JsonConvert.DeserializeObject<ShoutoutConfig>(File.ReadAllText(path));
            }
            var config = new ShoutoutConfig();
            File.WriteAllText(path, JsonConvert.SerializeObject(config, Formatting.Indented));
            return config;
        }

        public void Save(string path)
        {
            File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
        }
    }
} 