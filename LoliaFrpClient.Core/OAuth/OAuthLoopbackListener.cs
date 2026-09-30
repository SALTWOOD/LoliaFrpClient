using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;

namespace LoliaFrpClient.Core;

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

    public OAuthLoopbackListener(OAuthOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _path = options.CallbackPath;

        var bound = new List<TcpListener>(2);
        foreach (var address in new[] { IPAddress.IPv6Loopback, IPAddress.Loopback })
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

        if (bound.Count == 0) throw new IOException($"端口 {options.CallbackPort} 上无法监听回调地址,可能已被占用。");

        _listeners = [.. bound];

        foreach (var listener in _listeners) _ = AcceptLoopAsync(listener, _shutdown.Token);
    }

    public ValueTask DisposeAsync()
    {
        _shutdown.Cancel();
        foreach (var listener in _listeners) listener.Stop();

        _shutdown.Dispose();
        return ValueTask.CompletedTask;
    }

    public async Task<OAuthCallback> WaitForCallbackAsync(TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        using var timeoutSource = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);

        try
        {
            return await _callbacks.Reader.ReadAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested &&
                                                 !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("等待授权回调超时,请重试。");
        }
    }

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
                // 咕咕咕……
            }
            finally
            {
                client.Dispose();
            }
        }
    }

    private async Task<OAuthCallback?> HandleAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using var stream = client.GetStream();

        var requestLine = await ReadRequestLineAsync(stream, cancellationToken).ConfigureAwait(false);
        if (requestLine is null) return null;

        var segments = requestLine.Split(' ');
        if (segments.Length < 2) return null;

        var target = segments[1];
        var separator = target.IndexOf('?');
        var path = separator < 0 ? target : target[..separator];
        var query = separator < 0 ? string.Empty : target[(separator + 1)..];

        if (!string.Equals(path, _path, StringComparison.OrdinalIgnoreCase))
        {
            await WriteResponseAsync(stream, 404, "Not Found", "<p>Not Found</p>", cancellationToken)
                .ConfigureAwait(false);
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

    private static async Task<string?> ReadRequestLineAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var received = new List<byte>(1024);
        var buffer = new byte[1024];

        while (received.Count < 16384)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0) break;

            for (var i = 0; i < read; i++) received.Add(buffer[i]);

            if (HeadComplete(received)) break;
        }

        if (received.Count == 0) return null;

        var head = Encoding.ASCII.GetString(received.ToArray());
        var end = head.IndexOf('\r');
        if (end < 0) end = head.IndexOf('\n');

        var line = (end < 0 ? head : head[..end]).Trim();
        return line.Length == 0 ? null : line;
    }

    private static bool HeadComplete(List<byte> received)
    {
        var count = received.Count;

        if (count >= 4 &&
            received[count - 4] == (byte)'\r' && received[count - 3] == (byte)'\n' &&
            received[count - 2] == (byte)'\r' && received[count - 1] == (byte)'\n')
            return true;

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

        static string Decode(string value)
        {
            return Uri.UnescapeDataString(value.Replace('+', ' '));
        }
    }
}

public sealed record OAuthCallback(string? Code, string? State, string? Error, string? ErrorDescription);