using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;

namespace LoliaFrpClient.Core;

/// <summary>
///     OAuth 授权回调的接收器:在环回地址上起一个只认一个路径的极简 HTTP 服务,
///     等授权服务器把浏览器重定向回来,取出 <c>code</c> / <c>state</c>。
/// </summary>
/// <remarks>
///     <para>
///         不用 <see cref="HttpListener" />:它在 Windows 上要求预先用 <c>netsh http add urlacl</c>
///         注册前缀,否则非管理员进程直接抛异常。<see cref="TcpListener" /> 绑定环回地址不需要任何
///         权限,行为在 Windows 与 Linux 上一致。
///     </para>
///     <para>
///         IPv4 与 IPv6 的环回地址各监听一个:<c>localhost</c> 具体解析到哪个地址由系统决定,
///         只监听其中一个会让回调随机地连不上。某个地址族不可用时(例如系统禁用了 IPv6)退化即可。
///     </para>
/// </remarks>
public sealed class OAuthLoopbackListener : IAsyncDisposable
{
    private const string ResponseStyle =
        "font-family:system-ui,-apple-system,'Segoe UI',sans-serif;" +
        "background:#fafafa;color:#1a1a1a;display:flex;align-items:center;" +
        "justify-content:center;height:100vh;margin:0;text-align:center";

    private readonly Channel<OAuthCallback> _callbacks = Channel.CreateUnbounded<OAuthCallback>();
    private readonly TcpListener[] _listeners;
    private readonly string _path;
    private readonly CancellationTokenSource _shutdown = new();

    /// <summary>按 OAuth 配置在约定端口上开始监听。</summary>
    /// <param name="options">OAuth 配置,提供端口与回调路径。</param>
    /// <exception cref="IOException">IPv4 与 IPv6 环回地址都绑定失败(通常是端口被占用)。</exception>
    public OAuthLoopbackListener(OAuthOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _path = options.CallbackPath;

        var bound = new List<TcpListener>(2);
        foreach (var address in new[] { IPAddress.IPv6Loopback, IPAddress.Loopback })
        {
            try
            {
                var listener = new TcpListener(address, options.CallbackPort);
                listener.Start();
                bound.Add(listener);
            }
            catch (SocketException)
            {
                // 该地址族不可用,另一个仍然够用。
            }
        }

        if (bound.Count == 0)
        {
            throw new IOException($"端口 {options.CallbackPort} 上无法监听回调地址,可能已被占用。");
        }

        _listeners = [.. bound];

        foreach (var listener in _listeners)
        {
            _ = AcceptLoopAsync(listener, _shutdown.Token);
        }
    }

    /// <summary>释放监听端口。</summary>
    public ValueTask DisposeAsync()
    {
        _shutdown.Cancel();
        foreach (var listener in _listeners)
        {
            listener.Stop();
        }

        _shutdown.Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>等待授权服务器把浏览器重定向回来。</summary>
    /// <param name="timeout">等待上限。用户可能在浏览器里停留很久,不宜过短。</param>
    /// <param name="cancellationToken">取消标记。</param>
    /// <returns>回调参数。<c>Code</c> 与 <c>Error</c> 必有一个非空。</returns>
    /// <exception cref="TimeoutException">超时仍未收到回调。</exception>
    public async Task<OAuthCallback> WaitForCallbackAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        using var timeoutSource = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);

        try
        {
            return await _callbacks.Reader.ReadAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("等待授权回调超时,请重试。");
        }
    }

    /// <summary>
    ///     每个监听地址一个循环。浏览器可能为一次授权发起多个连接(预连接、favicon 等),
    ///     所以这里持续接受连接,直到某个连接真正带回授权码。
    /// </summary>
    private async Task AcceptLoopAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // 监听器已被 Stop,或整个对象正在释放。
                return;
            }

            try
            {
                var callback = await HandleAsync(client, cancellationToken).ConfigureAwait(false);
                if (callback is not null)
                {
                    _callbacks.Writer.TryWrite(callback);
                    return;
                }
            }
            catch (Exception)
            {
                // 单个连接出错(客户端提前断开等)不应影响继续等待。
            }
            finally
            {
                client.Dispose();
            }
        }
    }

    /// <summary>处理一个连接。不是回调路径时回 404 并返回 <c>null</c>,让循环继续等。</summary>
    private async Task<OAuthCallback?> HandleAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using var stream = client.GetStream();

        var requestLine = await ReadRequestLineAsync(stream, cancellationToken).ConfigureAwait(false);
        if (requestLine is null)
        {
            return null;
        }

        // 形如 "GET /callback?code=...&state=... HTTP/1.1"
        var segments = requestLine.Split(' ');
        if (segments.Length < 2)
        {
            return null;
        }

        var target = segments[1];
        var separator = target.IndexOf('?');
        var path = separator < 0 ? target : target[..separator];
        var query = separator < 0 ? string.Empty : target[(separator + 1)..];

        if (!string.Equals(path, _path, StringComparison.OrdinalIgnoreCase))
        {
            await WriteResponseAsync(stream, 404, "Not Found", "<p>Not Found</p>", cancellationToken).ConfigureAwait(false);
            return null;
        }

        var parameters = ParseQuery(query);
        parameters.TryGetValue("code", out var code);
        parameters.TryGetValue("state", out var state);
        parameters.TryGetValue("error", out var error);
        parameters.TryGetValue("error_description", out var description);

        var body = string.IsNullOrEmpty(error)
            ? "<h2>授权成功</h2><p>请回到 LoliaFrp 客户端继续操作,本页可以关闭。</p>"
            : $"<h2>授权失败</h2><p>{WebUtility.HtmlEncode(description ?? error)}</p><p>请回到客户端重试。</p>";

        await WriteResponseAsync(stream, 200, "OK", body, cancellationToken).ConfigureAwait(false);

        return new OAuthCallback(code, state, error, description);
    }

    /// <summary>
    ///     读完整段请求头再返回首行。
    /// </summary>
    /// <remarks>
    ///     必须读到空行为止,不能只取首行就收工:套接字上留着未读数据时关闭连接,
    ///     TCP 会发 RST 而不是 FIN,客户端会认为响应被截断并报错
    ///     (<c>Error while copying content to a stream</c>)。
    /// </remarks>
    private static async Task<string?> ReadRequestLineAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var received = new List<byte>(1024);
        var buffer = new byte[1024];

        while (received.Count < 16384)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            for (var i = 0; i < read; i++)
            {
                received.Add(buffer[i]);
            }

            if (HeadComplete(received))
            {
                break;
            }
        }

        if (received.Count == 0)
        {
            return null;
        }

        var head = Encoding.ASCII.GetString(received.ToArray());
        var end = head.IndexOf('\r');
        if (end < 0)
        {
            end = head.IndexOf('\n');
        }

        var line = (end < 0 ? head : head[..end]).Trim();
        return line.Length == 0 ? null : line;
    }

    /// <summary>请求头是否以空行结束(CRLF CRLF,或宽松的 LF LF)。</summary>
    private static bool HeadComplete(List<byte> received)
    {
        var count = received.Count;

        if (count >= 4 &&
            received[count - 4] == (byte)'\r' && received[count - 3] == (byte)'\n' &&
            received[count - 2] == (byte)'\r' && received[count - 1] == (byte)'\n')
        {
            return true;
        }

        return count >= 2 && received[count - 2] == (byte)'\n' && received[count - 1] == (byte)'\n';
    }

    private static async Task WriteResponseAsync(
        NetworkStream stream,
        int status,
        string reason,
        string body,
        CancellationToken cancellationToken)
    {
        var payload = Encoding.UTF8.GetBytes(
            $"<!doctype html><html lang=\"zh-CN\"><head><meta charset=\"utf-8\">" +
            $"<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">" +
            $"<title>LoliaFrp</title></head><body style=\"{ResponseStyle}\">" +
            $"<div>{body}</div></body></html>");

        var header = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 {status} {reason}\r\n" +
            "Content-Type: text/html; charset=utf-8\r\n" +
            $"Content-Length: {payload.Length}\r\n" +
            "Cache-Control: no-store\r\n" +
            "Connection: close\r\n\r\n");

        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            var key = separator < 0 ? pair : pair[..separator];
            var value = separator < 0 ? string.Empty : pair[(separator + 1)..];

            // 浏览器可能按 application/x-www-form-urlencoded 把空格编成 '+',解码前先还原。
            result[Decode(key)] = Decode(value);
        }

        return result;

        static string Decode(string value) => Uri.UnescapeDataString(value.Replace('+', ' '));
    }
}

/// <summary>授权回调上带回来的参数。</summary>
/// <param name="Code">授权码。授权被拒绝时为 <c>null</c>。</param>
/// <param name="State">发起授权时生成的不透明串,换取令牌时需回传校验。</param>
/// <param name="Error">授权服务器返回的错误码,成功时为 <c>null</c>。</param>
/// <param name="ErrorDescription">错误的可读描述。</param>
public sealed record OAuthCallback(string? Code, string? State, string? Error, string? ErrorDescription);
