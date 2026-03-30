using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Windows.Storage;

namespace LoliaFrpClient.Services;

public class SettingsStorage
{
    private static readonly Lazy<SettingsStorage> _instance = new(() => new SettingsStorage());
    public static SettingsStorage Instance => _instance.Value;

    private readonly ApplicationDataContainer? _localSettings;
    private readonly string? _settingsFilePath;
    private readonly Dictionary<string, object?> _fileCache;
    private readonly bool _isPackaged;

    private readonly Dictionary<string, object> _defaults = new()
    {
        { nameof(IsDarkMode), false },
        { nameof(AutoCheckClientUpdate), true },
        { nameof(DownloadUrlTemplate), "https://github.com/{owner}/{repo}/releases/download/{tag}/{asset}" },
        { nameof(ApiBaseUrl), "https://api.lolia.io" } // Example default
    };

    private SettingsStorage()
    {
        _isPackaged = Utils.IsPackaged();

        if (_isPackaged)
        {
            _localSettings = ApplicationData.Current.LocalSettings;
            _fileCache = [];
        }
        else
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LoliaFrpClient");
            Directory.CreateDirectory(path);
            _settingsFilePath = Path.Combine(path, "settings.json");
            _fileCache = LoadFromFile();
        }
    }

    #region Properties

    public bool IsDarkMode { get => Read(nameof(IsDarkMode), (bool)_defaults[nameof(IsDarkMode)]); set => Write(nameof(IsDarkMode), value); }
    public string? OAuthToken { get => Read<string?>("Authorization"); set => Write("Authorization", value); }
    public string? RefreshToken { get => Read<string?>("RefreshToken"); set => Write("RefreshToken", value); }
    public string? ApiBaseUrl { get => Read(nameof(ApiBaseUrl), (string)_defaults[nameof(ApiBaseUrl)]); set => Write(nameof(ApiBaseUrl), value); }
    public bool AutoCheckClientUpdate { get => Read(nameof(AutoCheckClientUpdate), (bool)_defaults[nameof(AutoCheckClientUpdate)]); set => Write(nameof(AutoCheckClientUpdate), value); }
    
    // New Template property to replace MirrorType
    public string DownloadUrlTemplate { get => Read(nameof(DownloadUrlTemplate), (string)_defaults[nameof(DownloadUrlTemplate)]); set => Write(nameof(DownloadUrlTemplate), value); }

    #endregion

    public T Read<T>(string key, T defaultValue = default!)
    {
        if (_isPackaged)
        {
            return _localSettings!.Values.TryGetValue(key, out var val) && val is T typedVal ? typedVal : defaultValue;
        }

        if (_fileCache.TryGetValue(key, out var raw))
        {
            if (raw is T alreadyTyped) return alreadyTyped;
            if (raw is JsonElement json) return JsonSerializer.Deserialize<T>(json.GetRawText()) ?? defaultValue;
        }
        return defaultValue;
    }

    public void Write<T>(string key, T value)
    {
        if (_isPackaged)
        {
            _localSettings!.Values[key] = value;
        }
        else
        {
            _fileCache[key] = value;
            SaveToFile();
        }
    }

    private Dictionary<string, object?> LoadFromFile()
    {
        if (!File.Exists(_settingsFilePath)) return [];
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(File.ReadAllText(_settingsFilePath!)) ?? [];
        }
        catch { return []; }
    }

    private void SaveToFile() 
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(_settingsFilePath!, JsonSerializer.Serialize(_fileCache, options));
    }

    public void Clear()
    {
        if (_isPackaged) _localSettings!.Values.Clear();
        else { _fileCache.Clear(); SaveToFile(); }
    }
}