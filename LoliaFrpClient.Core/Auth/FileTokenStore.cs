using System.Text.Json;
using System.Text.Json.Serialization;

namespace LoliaFrpClient.Core;

/// <summary>
///     基于本地 JSON 文件的凭证存储。默认落在
///     <c>%LOCALAPPDATA%\LoliaFrpClient\tokens.json</c>。
/// </summary>
/// <remarks>
///     令牌以明文写入磁盘。若后续需要更高安全性,应改为 DPAPI 加密后存储,
///     或由宿主应用提供自己的 <see cref="ITokenStore" /> 实现。
/// </remarks>
public sealed class FileTokenStore : ITokenStore
{
    private readonly object _gate = new();
    private readonly string _path;
    private string? _accessToken;
    private TokenOrigin _origin;
    private string? _refreshToken;

    /// <summary>使用默认路径创建存储。</summary>
    public FileTokenStore() : this(DefaultPath())
    {
    }

    /// <summary>使用指定文件路径创建存储。</summary>
    /// <param name="path">凭证文件路径。所在目录会被自动创建。</param>
    public FileTokenStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
        Load();
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <summary>令牌文件的默认位置。</summary>
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
            if (!File.Exists(_path))
            {
                return;
            }

            var snapshot = JsonSerializer.Deserialize(File.ReadAllText(_path), TokenStoreJsonContext.Default.TokenSnapshot);
            if (snapshot is null)
            {
                return;
            }

            _accessToken = snapshot.AccessToken;
            _refreshToken = snapshot.RefreshToken;
            _origin = snapshot.Origin;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // 凭证文件损坏或不可读时按未登录处理,不阻断启动。
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
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

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
            // 落盘失败不应让内存中的会话失效;下次写入会重试。
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
