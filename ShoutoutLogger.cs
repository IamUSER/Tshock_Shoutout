using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace ShoutoutPlugin
{
    public class ShoutoutLogger
    {
        private readonly ShoutoutConfig _config;
        private readonly string _baseLogPath;
        private long _currentFileSize;
        private readonly object _lockObject = new object();

        public ShoutoutLogger(ShoutoutConfig config)
        {
            _config = config;
            _baseLogPath = config.LogFilePath;
            _currentFileSize = File.Exists(_baseLogPath) ? new FileInfo(_baseLogPath).Length : 0;
        }

        public async Task LogShoutoutAsync(string playerName, string message, DateTime timestamp)
        {
            var entry = new ShoutoutEntry
            {
                Timestamp = timestamp,
                PlayerName = playerName,
                Message = message
            };

            string logLine = FormatLogEntry(entry);
            
            await WriteToLogAsync(logLine);
        }

        private string FormatLogEntry(ShoutoutEntry entry)
        {
            switch (_config.LogFormat.ToLower())
            {
                case "json":
                    return JsonConvert.SerializeObject(entry) + Environment.NewLine;
                case "csv":
                    return $"{entry.Timestamp:yyyy-MM-dd HH:mm:ss},{entry.PlayerName},{entry.Message}{Environment.NewLine}";
                default: // txt
                    return $"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss}] {entry.PlayerName}: {entry.Message}{Environment.NewLine}";
            }
        }

        private async Task WriteToLogAsync(string logLine)
        {
            lock (_lockObject)
            {
                if (_currentFileSize >= _config.LogRotationSizeMB * 1024 * 1024)
                {
                    RotateLogs();
                }

                File.AppendAllText(_baseLogPath, logLine);
                _currentFileSize += Encoding.UTF8.GetByteCount(logLine);
            }
        }

        private void RotateLogs()
        {
            // Delete oldest log if we've reached max files
            string oldestLog = $"{_baseLogPath}.{_config.MaxLogFiles}";
            if (File.Exists(oldestLog))
            {
                File.Delete(oldestLog);
            }

            // Shift existing logs
            for (int i = _config.MaxLogFiles - 1; i >= 1; i--)
            {
                string currentFile = $"{_baseLogPath}.{i}";
                string nextFile = $"{_baseLogPath}.{i + 1}";
                if (File.Exists(currentFile))
                {
                    File.Move(currentFile, nextFile);
                }
            }

            // Move current log
            if (File.Exists(_baseLogPath))
            {
                File.Move(_baseLogPath, $"{_baseLogPath}.1");
            }

            _currentFileSize = 0;
        }
    }
} 