using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Windows.Storage;

namespace LoliaFrpClient.Services
{
    /// <summary>
    /// Settings Storage service class for storing user data and dark mode settings
    /// </summary>
    public class SettingsStorage
    {
        private static readonly Lazy<SettingsStorage> _instance = new Lazy<SettingsStorage>(() => new SettingsStorage());
        public static SettingsStorage Instance => _instance.Value;

        private readonly ApplicationDataContainer? _localSettings;
        private readonly bool _useFileStorage;
        private readonly string _settingsFilePath;
        private Dictionary<string, object?> _fileSettings;

        private SettingsStorage()
        {
            if (Utils.IsPackaged())
            {
                _localSettings = ApplicationData.Current.LocalSettings;
                _useFileStorage = false;
                _settingsFilePath = string.Empty;
                _fileSettings = new Dictionary<string, object?>();
            }
            else
            {
                _localSettings = null;
                _useFileStorage = true;

                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var appFolder = Path.Combine(appDataPath, "LoliaFrpClient");

                if (!Directory.Exists(appFolder))
                {
                    Directory.CreateDirectory(appFolder);
                }

                _settingsFilePath = Path.Combine(appFolder, "settings.json");
                _fileSettings = LoadSettingsFromFile();
            }
        }

        /// <summary>
        /// Load settings from a file
        /// </summary>
        private Dictionary<string, object?> LoadSettingsFromFile()
        {
            if (!File.Exists(_settingsFilePath))
            {
                return new Dictionary<string, object?>();
            }

            try
            {
                var json = File.ReadAllText(_settingsFilePath);
                return JsonSerializer.Deserialize<Dictionary<string, object?>>(json)
                    ?? new Dictionary<string, object?>();
            }
            catch
            {
                return new Dictionary<string, object?>();
            }
        }

        /// <summary>
        /// Save settings to a file
        /// </summary>
        private void SaveSettingsToFile()
        {
            var json = JsonSerializer.Serialize(_fileSettings, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(_settingsFilePath, json);
        }

        /// <summary>
        /// Dark mode setting
        /// </summary>
        public bool IsDarkMode
        {
            get => Read<bool>("IsDarkMode", false);
            set => Write("IsDarkMode", value);
        }

        /// <summary>
        /// OAuth token
        /// </summary>
        public string? OAuthToken
        {
            get => Read<string?>("Authorization", null);
            set => Write("Authorization", value);
        }

        /// <summary>
        /// Refresh token
        /// </summary>
        public string? RefreshToken
        {
            get => Read<string?>("RefreshToken", null);
            set => Write("RefreshToken", value);
        }

        /// <summary>
        /// API Base URL
        /// </summary>
        public string? ApiBaseUrl
        {
            get => Read<string?>("ApiBaseUrl", null);
            set => Write("ApiBaseUrl", value);
        }

        /// <summary>
        /// Read setting
        /// </summary>
        public T Read<T>(string key, T defaultValue = default!)
        {
            if (_useFileStorage)
            {
                if (_fileSettings.TryGetValue(key, out var value))
                {
                    if (value is JsonElement jsonElement)
                    {
                        return JsonSerializer.Deserialize<T>(jsonElement.GetRawText()) ?? defaultValue;
                    }
                    if (value is T typedValue)
                    {
                        return typedValue;
                    }
                }
                return defaultValue;
            }
            else
            {
                if (_localSettings!.Values.TryGetValue(key, out var value))
                {
                    if (value is T typedValue)
                    {
                        return typedValue;
                    }
                }
                return defaultValue;
            }
        }

        /// <summary>
        /// Write setting
        /// </summary>
        public void Write<T>(string key, T value)
        {
            if (_useFileStorage)
            {
                _fileSettings[key] = value;
                SaveSettingsToFile();
            }
            else
            {
                _localSettings!.Values[key] = value;
            }
        }

        /// <summary>
        /// Delete setting
        /// </summary>
        public void Delete(string key)
        {
            if (_useFileStorage)
            {
                _fileSettings.Remove(key);
                SaveSettingsToFile();
            }
            else
            {
                _localSettings!.Values.Remove(key);
            }
        }

        /// <summary>
        /// Clear all settings
        /// </summary>
        public void Clear()
        {
            if (_useFileStorage)
            {
                _fileSettings.Clear();
                SaveSettingsToFile();
            }
            else
            {
                _localSettings!.Values.Clear();
            }
        }
    }
}
