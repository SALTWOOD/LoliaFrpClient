using System;
using System.Collections.Generic;

namespace LoliaFrpClient.Services;

// Hard bounds are the entire point: frpc can emit thousands of lines per second while
// reconnecting, and unbounded growth would exhaust memory.
internal sealed class FrpcLogBuffer
{
    public const int MaxLineLength = 1000;

    public const int MaxPendingLines = 4000;

    private readonly object _gate = new();
    private readonly Queue<string> _pending = new();
    private long _dropped;

    public int PendingCount
    {
        get
        {
            lock (_gate)
            {
                return _pending.Count;
            }
        }
    }

    public void Append(string? line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;

        var text = line.Length > MaxLineLength ? line[..MaxLineLength] + " …(已截断)" : line;

        // Tabs misalign everything after them in a monospace panel.
        text = text.Replace('\t', ' ').TrimEnd();

        lock (_gate)
        {
            // Drop the newest rather than the oldest: the cause of a flood is almost always in
            // its first few lines, so evicting the oldest would throw away the useful part.
            if (_pending.Count >= MaxPendingLines)
            {
                _dropped++;
                return;
            }

            _pending.Enqueue(text);
        }
    }

    public List<string> Drain(int max)
    {
        lock (_gate)
        {
            if (_pending.Count == 0 && _dropped == 0) return [];

            var take = Math.Min(_pending.Count, max);
            var result = new List<string>(take + 1);

            if (_dropped > 0)
            {
                result.Add($"…输出过快,已丢弃 {_dropped} 行");
                _dropped = 0;
            }

            for (var i = 0; i < take; i++) result.Add(_pending.Dequeue());

            return result;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _pending.Clear();
            _dropped = 0;
        }
    }
}