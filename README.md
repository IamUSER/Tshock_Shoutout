# Shoutout Plugin for TShock

A feature-rich TShock plugin that allows players to leave shoutouts with advanced logging, statistics tracking, and administration features. Perfect for servers that want to encourage player interaction and keep track of community engagement.

## Features

- Player shoutout system with configurable cooldowns and limits
- Advanced logging with multiple format options (TXT, JSON, CSV)
- Automatic log rotation to prevent large files
- Configurable reminder system
- Statistics tracking (top users, peak hours, etc.)
- Rate limiting and spam protection
- Sound effects for shoutouts (configurable)
- Colored messages
- Admin commands for monitoring and management
- Secure input handling and sanitization
- View recent shoutouts in-game

## Installation

1. Download the latest `ShoutoutPlugin.dll` release
2. Place the DLL in your TShock server's `ServerPlugins` folder
3. Restart your TShock server
4. The plugin will automatically create its configuration files on first run:
   - `ShoutoutConfig.json` - Plugin configuration
   - `ShoutoutStats.json` - Statistics data

## Commands

### Player Commands
- `/shoutout <message>` - Leave a shoutout message
  - Example: `/shoutout Having a great time on the server!`
- `/shoutouts [number]` - View recent shoutouts (defaults to 10, max 50)
  - Example: `/shoutouts` - Shows last 10 shoutouts
  - Example: `/shoutouts 5` - Shows last 5 shoutouts

### Admin Commands
- `/shoutoutadmin stats` - View shoutout statistics
- `/shoutoutadmin config <setting> <value>` - Modify configuration settings
- `/shoutoutadmin clear` - Clear shoutout cache

## Permissions

- `shoutout.use` - Allows use of the `/shoutout` command
- `shoutout.view` - Allows use of the `/shoutouts` command to view recent shoutouts
- `shoutout.admin` - Allows use of all `/shoutoutadmin` commands

## Configuration

The `ShoutoutConfig.json` file contains the following settings:

```json
{
  "ReminderIntervalMinutes": 60,
  "ReminderMessage": "Let us know you were here! /shoutout",
  "MaxMessageLength": 200,
  "LogFilePath": "shoutouts.log",
  "LogFormat": "txt",
  "LogRotationSizeMB": 10,
  "MaxLogFiles": 5,
  "CooldownSeconds": 60,
  "EnableSounds": true,
  "MessageColor": "Yellow",
  "MaxShoutoutsPerDay": 10,
  "RequireApproval": false,
  "EnableAnonymous": true,
  "CacheSize": 100
}
```

### Configuration Options

- `ReminderIntervalMinutes`: How often to broadcast the reminder message (in minutes)
- `ReminderMessage`: The message to broadcast reminding players about shoutouts
- `MaxMessageLength`: Maximum length of shoutout messages
- `LogFilePath`: Path to the log file
- `LogFormat`: Log format (txt, json, or csv)
- `LogRotationSizeMB`: Size in MB before rotating log files
- `MaxLogFiles`: Maximum number of log files to keep
- `CooldownSeconds`: Time players must wait between shoutouts
- `EnableSounds`: Whether to play sounds when shoutouts are made
- `MessageColor`: Color of shoutout messages (any valid Terraria color)
- `MaxShoutoutsPerDay`: Maximum number of shoutouts per player per day
- `RequireApproval`: Whether shoutouts require admin approval
- `EnableAnonymous`: Allow anonymous shoutouts
- `CacheSize`: Number of recent shoutouts to keep in memory

## Statistics

The plugin tracks various statistics including:
- Total number of shoutouts
- Top users and their shoutout counts
- Peak hours for shoutout activity
- Per-player daily statistics

Statistics are automatically saved and can be viewed using `/shoutoutadmin stats`.

## Security Features

- Input sanitization to prevent malicious content
- Rate limiting to prevent spam
- Configurable daily limits
- Proper permission checks
- Secure data storage
- Resource cleanup on player disconnect

## Support

If you encounter any issues or have suggestions, please:
1. Check your TShock logs for error messages
2. Verify your configuration settings
3. Ensure all permissions are set correctly
4. Create an issue on the GitHub repository with details about your problem

## Credits

This plugin was imagined by IamUSER and developed by Cascade, with assistance from the Claude AI assistant.

## License

This plugin is released under the MIT License. Feel free to modify and distribute it according to your needs.
