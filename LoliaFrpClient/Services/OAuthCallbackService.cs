using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using LoliaFrpClient.Constants;

namespace LoliaFrpClient.Services;

/// <summary>
///     OAuth 回调监听服务，用于监听 OAuth 授权回调
/// </summary>
public class OAuthCallbackService
{
    private readonly object _lock = new();
    private CancellationTokenSource? _cancellationTokenSource;
    private HttpListener? _listener;
    private Task? _listenerTask;

    /// <summary>
    ///     授权完成事件
    /// </summary>
    public event EventHandler<OAuthCallbackResult>? AuthorizationCompleted;

    /// <summary>
    ///     启动 HTTP 服务器监听回调
    /// </summary>
    public async Task StartAsync()
    {
        lock (_lock)
        {
            if (_listener != null && _listener.IsListening) return; // 已经在监听中
        }

        try
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{OAuthConstants.CallbackPort}{OAuthConstants.CallbackPath}/");
            _listener.Start();

            _cancellationTokenSource = new CancellationTokenSource();
            _listenerTask = Task.Run(() => ListenForRequests(_cancellationTokenSource.Token));

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Stop();
            throw new Exception($"无法启动 OAuth 回调监听服务: {ex.Message}", ex);
        }
    }

    /// <summary>
    ///     停止 HTTP 服务器
    /// </summary>
    public void Stop()
    {
        lock (_lock)
        {
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;
            }

            if (_listener != null)
            {
                try
                {
                    _listener.Stop();
                    _listener.Close();
                }
                catch
                {
                    // 忽略停止时的异常
                }

                _listener = null;
            }

            if (_listenerTask != null)
            {
                try
                {
                    _listenerTask.Wait(TimeSpan.FromSeconds(2));
                }
                catch
                {
                    // 忽略等待任务完成的异常
                }

                _listenerTask = null;
            }
        }
    }

    /// <summary>
    ///     监听请求
    /// </summary>
    private async Task ListenForRequests(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
            try
            {
                if (_listener == null || !_listener.IsListening) break;

                var context = await _listener.GetContextAsync().ConfigureAwait(false);
                _ = Task.Run(() => HandleRequest(context), cancellationToken);
            }
            catch (HttpListenerException)
            {
                // 监听器已停止
                break;
            }
            catch (OperationCanceledException)
            {
                // 操作已取消
                break;
            }
            catch (Exception)
            {
                // 忽略其他异常
                break;
            }
    }

    /// <summary>
    ///     处理请求
    /// </summary>
    private void HandleRequest(HttpListenerContext context)
    {
        try
        {
            var request = context.Request;
            var response = context.Response;

            // 检查是否是回调请求
            if (request.Url?.AbsolutePath == OAuthConstants.CallbackPath ||
                request.Url?.AbsolutePath == OAuthConstants.CallbackPath + "/")
            {
                // 获取查询参数
                var queryString = request.Url.Query;
                var callbackResult = ExtractOAuthResultFromQuery(queryString);

                // 根据结果返回不同的页面
                string responseHtml;
                if (callbackResult.Error != null)
                    responseHtml = GetErrorHtml(callbackResult.Error, callbackResult.ErrorDescription);
                else
                    responseHtml = GetSuccessHtml();

                var buffer = Encoding.UTF8.GetBytes(responseHtml);
                response.ContentLength64 = buffer.Length;
                response.ContentType = "text/html; charset=utf-8";
                response.StatusCode = 200;
                response.OutputStream.Write(buffer, 0, buffer.Length);
                response.OutputStream.Close();

                // 触发授权完成事件
                AuthorizationCompleted?.Invoke(this, callbackResult);
            }
            else
            {
                // 返回 404
                response.StatusCode = 404;
                response.Close();
            }
        }
        catch (Exception)
        {
            // 忽略处理请求时的异常
        }
    }

    /// <summary>
    ///     从查询字符串中提取 OAuth 结果
    /// </summary>
    private OAuthCallbackResult ExtractOAuthResultFromQuery(string queryString)
    {
        var result = new OAuthCallbackResult();

        if (string.IsNullOrEmpty(queryString)) return result;

        // 解析查询参数
        var parameters = HttpUtility.ParseQueryString(queryString);
        result.Code = parameters["code"];
        result.State = parameters["state"];
        result.Error = parameters["error"];
        result.ErrorDescription = parameters["error_description"];

        return result;
    }

    /// <summary>
    ///     获取成功页面 HTML
    /// </summary>
    private string GetSuccessHtml()
    {
        return @"<!DOCTYPE html>
<html lang=""zh-CN"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>授权成功</title>
</head>
<body>
    <h1>授权成功</h1>
    <p>您现在可以关闭此页面并返回应用程序</p>
</body>
</html>";
    }

    /// <summary>
    ///     获取错误页面 HTML
    /// </summary>
    private string GetErrorHtml(string error, string? errorDescription)
    {
        var description = string.IsNullOrEmpty(errorDescription) ? "授权过程中发生错误" : errorDescription;
        return $@"<!DOCTYPE html>
<html lang=""zh-CN"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>授权失败</title>
    <style>
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, Cantarell, sans-serif;
            display: flex;
            justify-content: center;
            align-items: center;
            height: 100vh;
            margin: 0;
            background: linear-gradient(135deg, #f093fb 0%, #f5576c 100%);
        }}
        .container {{
            background: white;
            padding: 40px;
            border-radius: 12px;
            box-shadow: 0 10px 40px rgba(0, 0, 0, 0.2);
            text-align: center;
            max-width: 400px;
        }}
        .error-icon {{
            font-size: 64px;
            margin-bottom: 20px;
        }}
        h1 {{
            color: #333;
            margin: 0 0 16px 0;
            font-size: 24px;
        }}
        p {{
            color: #666;
            margin: 0 0 24px 0;
            line-height: 1.6;
        }}
        .error-code {{
            background: #fee;
            padding: 12px;
            border-radius: 6px;
            font-size: 14px;
            color: #c33;
            font-weight: bold;
            margin-bottom: 12px;
        }}
        .note {{
            background: #f0f0f0;
            padding: 12px;
            border-radius: 6px;
            font-size: 14px;
            color: #888;
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""error-icon"">✕</div>
        <h1>授权失败</h1>
        <div class=""error-code"">{error}</div>
        <p>{description}</p>
        <div class=""note"">请关闭此页面并重试。</div>
    </div>
</body>
</html>";
    }

    /// <summary>
    ///     获取回调 URL
    /// </summary>
    public static string GetCallbackUrl()
    {
        return $"http://localhost:{OAuthConstants.CallbackPort}{OAuthConstants.CallbackPath}";
    }

    /// <summary>
    ///     OAuth 回调结果
    /// </summary>
    public class OAuthCallbackResult
    {
        public string? Code { get; set; }
        public string? State { get; set; }
        public string? Error { get; set; }
        public string? ErrorDescription { get; set; }
    }
}