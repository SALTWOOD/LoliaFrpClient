using System;
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

        private readonly ApplicationDataContainer _localSettings;

        private SettingsStorage()
        {
            _localSettings = ApplicationData.Current.LocalSettings;
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
        /// Read setting
        /// </summary>
        public T Read<T>(string key, T defaultValue = default!)
        {
            if (_localSettings.Values.TryGetValue(key, out var value))
            {
                if (value is T typedValue)
                {
                    return typedValue;
                }
            }
            return defaultValue;
        }

        /// <summary>
        /// Write setting
        /// </summary>
        public void Write<T>(string key, T value)
        {
            _localSettings.Values[key] = value;
        }

        /// <summary>
        /// Delete setting
        /// </summary>
        public void Delete(string key)
        {
            _localSettings.Values.Remove(key);
        }

        /// <summary>
        /// Clear all settings
        /// </summary>
        public void Clear()
        {
            _localSettings.Values.Clear();
        }
    }
}
