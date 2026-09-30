using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace LoliaFrpClient.Services;

// The avatar is decorative: every failure falls back to the initial letter, so failures are
// swallowed rather than surfaced in the UI.
internal sealed class AvatarLoader : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly HttpClient _http = new() { Timeout = Timeout };

    // One avatar is ever on screen, so a single-entry cache is enough.
    private string? _cachedUrl;
    private Bitmap? _cached;

    public async Task<Bitmap?> LoadAsync(string? url, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;

        if (string.Equals(url, _cachedUrl, StringComparison.Ordinal)) return _cached;

        try
        {
            var bytes = await _http.GetByteArrayAsync(url, ct).ConfigureAwait(false);

            using var stream = new MemoryStream(bytes);
            var bitmap = new Bitmap(stream);

            _cachedUrl = url;
            _cached = bitmap;
            return bitmap;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // Deliberately not cached: a transient network failure should be retried the next
            // time the page is opened.
            return null;
        }
    }

    public void Dispose()
    {
        _http.Dispose();
        _cached?.Dispose();
    }
}