using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LoliaFrpClient.Services;

// Deliberately a separate file from the token store: signing out wipes credentials wholesale,
// while these are user configuration and have to survive it.
internal sealed class AppSettings
{
    private static readonly object Gate = new();
    private static AppSettings? _current;

    private readonly string _path;

    private AppSettings(string path)
    {
        _path = path;
        Load();
    }

    public static AppSettings Current
    {
        get
        {
            lock (Gate)
            {
                return _current ??= new AppSettings(DefaultPath());
            }
        }
    }

    public string? FrpcPath { get; set; }

    public string? FrpcVersion { get; set; }

    public bool UseDownloadMirror { get; set; }

    public long FrpcLastUpdateCheckUtc { get; set; }

    public bool AutoCheckUpdates { get; set; } = true;

    public static string DefaultPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LoliaFrpClient",
            "settings.json");
    }

    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            var snapshot = new SettingsSnapshot
            {
                FrpcPath = FrpcPath,
                FrpcVersion = FrpcVersion,
                UseDownloadMirror = UseDownloadMirror,
                FrpcLastUpdateCheckUtc = FrpcLastUpdateCheckUtc,
                AutoCheckUpdates = AutoCheckUpdates
            };

            File.WriteAllText(_path, JsonSerializer.Serialize(snapshot, SettingsJsonContext.Default.SettingsSnapshot));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // A failed write must not invalidate the in-memory values; the next save retries.
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path)) return;

            var snapshot = JsonSerializer.Deserialize(
                File.ReadAllText(_path), SettingsJsonContext.Default.SettingsSnapshot);

            if (snapshot is null) return;

            FrpcPath = snapshot.FrpcPath;
            FrpcVersion = snapshot.FrpcVersion;
            UseDownloadMirror = snapshot.UseDownloadMirror;
            FrpcLastUpdateCheckUtc = snapshot.FrpcLastUpdateCheckUtc;
            // ?? true:老配置文件里没有这个键,缺省是开。若快照字段用不可空的 bool,
            // 反序列化会给 false,把「没配过」误当成「用户关掉了」。
            AutoCheckUpdates = snapshot.AutoCheckUpdates ?? true;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // A corrupt file is treated as fresh settings rather than blocking startup.
        }
    }

    internal sealed class SettingsSnapshot
    {
        [JsonPropertyName("frpc_path")] public string? FrpcPath { get; set; }

        [JsonPropertyName("frpc_version")] public string? FrpcVersion { get; set; }

        [JsonPropertyName("use_download_mirror")]
        public bool UseDownloadMirror { get; set; }

        [JsonPropertyName("frpc_last_update_check_utc")]
        public long FrpcLastUpdateCheckUtc { get; set; }

        // 可空:要区分「没有这个键」和「明确存了 false」。
        [JsonPropertyName("auto_check_updates")]
        public bool? AutoCheckUpdates { get; set; }
    }
}

[JsonSerializable(typeof(AppSettings.SettingsSnapshot))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext
{
}