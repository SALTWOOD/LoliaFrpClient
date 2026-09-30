namespace LoliaFrpClient.Core.Frpc;

public enum FrpcInstallStage
{
    FetchingRelease,
    Downloading,
    Verifying,
    Extracting,
    Completed
}

public sealed record FrpcInstallProgress
{
    public required FrpcInstallStage Stage { get; init; }

    public string Message { get; init; } = string.Empty;

    public long ReceivedBytes { get; init; }

    // -1 when the server sends no Content-Length, in which case Percent is meaningless.
    public long TotalBytes { get; init; } = -1;

    public double Percent { get; init; }

    public double SpeedBytesPerSecond { get; init; }
}

public sealed record FrpcInstallResult
{
    public required bool Success { get; init; }

    public string FrpcPath { get; init; } = string.Empty;

    public string Version { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public bool Canceled { get; init; }
}

public sealed record FrpcInstallOptions
{
    public bool UseMirror { get; init; }

    public string? Mirror { get; init; }

    public string? WorkDirectory { get; init; }
}