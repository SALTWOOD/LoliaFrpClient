using System.Text.Json;
using System.Text.Json.Serialization;

namespace LoliaFrpClient.Core;

public sealed class FileTokenStore : ITokenStore
{
    private readonly object _gate = new();
    private readonly string _path;
    private string? _accessToken;
    private TokenOrigin _origin;
    private string? _refreshToken;

    public FileTokenStore() : this(DefaultPath())
    {
    }

    public FileTokenStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
        Load();
    }

    public string? AccessToken
    {
        get
        {
            lock (_gate)
            {
                return _accessToken;
            }
        }
        set
        {
            lock (_gate)
            {
                _accessToken = value;
                Persist();
            }
        }
    }

    public string? RefreshToken
    {
        get
        {
            lock (_gate)
            {
                return _refreshToken;
            }
        }
        set
        {
            lock (_gate)
            {
                _refreshToken = value;
                Persist();
            }
        }
    }

    public TokenOrigin Origin
    {
        get
        {
            lock (_gate)
            {
                return _origin;
            }
        }
        set
        {
            lock (_gate)
            {
                _origin = value;
                Persist();
            }
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _accessToken = null;
            _refreshToken = null;
            _origin = TokenOrigin.None;
            Persist();
        }
    }

    public static string DefaultPath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LoliaFrpClient");
        return Path.Combine(dir, "tokens.json");
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path)) return;

            var snapshot =
                JsonSerializer.Deserialize(File.ReadAllText(_path), TokenStoreJsonContext.Default.TokenSnapshot);
            if (snapshot is null) return;

            _accessToken = snapshot.AccessToken;
            _refreshToken = snapshot.RefreshToken;
            _origin = snapshot.Origin;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            _accessToken = null;
            _refreshToken = null;
            _origin = TokenOrigin.None;
        }
    }

    private void Persist()
    {
        try
        {
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var snapshot = new TokenSnapshot
            {
                AccessToken = _accessToken,
                RefreshToken = _refreshToken,
                Origin = _origin
            };

            File.WriteAllText(_path, JsonSerializer.Serialize(snapshot, TokenStoreJsonContext.Default.TokenSnapshot));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    internal sealed class TokenSnapshot
    {
        [JsonPropertyName("access_token")] public string? AccessToken { get; set; }

        [JsonPropertyName("refresh_token")] public string? RefreshToken { get; set; }

        [JsonPropertyName("origin")] public TokenOrigin Origin { get; set; }
    }
}

[JsonSerializable(typeof(FileTokenStore.TokenSnapshot))]
internal sealed partial class TokenStoreJsonContext : JsonSerializerContext
{
}