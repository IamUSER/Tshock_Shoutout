using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace ShoutoutPlugin
{
    public class ShoutoutStats
    {
        public Dictionary<string, PlayerStats> PlayerStatistics { get; set; } = new Dictionary<string, PlayerStats>();
        public List<TimeSpan> PeakTimes { get; set; } = new List<TimeSpan>();
        public DateTime LastUpdate { get; set; } = DateTime.Now;
        public int TotalShoutouts { get; set; } = 0;

        public class PlayerStats
        {
            public int TotalShoutouts { get; set; } = 0;
            public DateTime LastShoutout { get; set; }
            public int ShoutoutsToday { get; set; } = 0;
            public DateTime LastDayReset { get; set; } = DateTime.Now.Date;
            public List<DateTime> ShoutoutHistory { get; set; } = new List<DateTime>();
        }

        public void UpdatePlayerStats(string playerName, DateTime timestamp)
        {
            if (!PlayerStatistics.ContainsKey(playerName))
            {
                PlayerStatistics[playerName] = new PlayerStats();
            }

            var stats = PlayerStatistics[playerName];
            
            // Reset daily stats if it's a new day
            if (DateTime.Now.Date != stats.LastDayReset)
            {
                stats.ShoutoutsToday = 0;
                stats.LastDayReset = DateTime.Now.Date;
            }

            stats.TotalShoutouts++;
            stats.ShoutoutsToday++;
            stats.LastShoutout = timestamp;
            stats.ShoutoutHistory.Add(timestamp);

            // Keep only last 100 entries in history
            if (stats.ShoutoutHistory.Count > 100)
            {
                stats.ShoutoutHistory.RemoveAt(0);
            }

            TotalShoutouts++;
            UpdatePeakTimes(timestamp.TimeOfDay);
        }

        private void UpdatePeakTimes(TimeSpan time)
        {
            PeakTimes.Add(time);
            if (PeakTimes.Count > 1000)
            {
                PeakTimes.RemoveAt(0);
            }
        }

        public Dictionary<string, int> GetTopUsers(int count = 10)
        {
            return PlayerStatistics
                .OrderByDescending(x => x.Value.TotalShoutouts)
                .Take(count)
                .ToDictionary(x => x.Key, x => x.Value.TotalShoutouts);
        }

        public List<TimeSpan> GetPeakHours()
        {
            return PeakTimes
                .GroupBy(t => new TimeSpan(t.Hours, 0, 0))
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .Take(5)
                .ToList();
        }

        public static ShoutoutStats Load(string path)
        {
            if (File.Exists(path))
            {
                return JsonConvert.DeserializeObject<ShoutoutStats>(File.ReadAllText(path));
            }
            return new ShoutoutStats();
        }

        public void Save(string path)
        {
            File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
        }
    }
} 