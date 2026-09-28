using System.Collections.Generic;

namespace LoliaFrpClient.ViewModels;

/// <summary>
/// 设置页。当前只有静态占位,尚未接入 OAuth 登录与 frpc 安装流程。
/// </summary>
public sealed class SettingsViewModel : ViewModelBase
{
    /// <summary>占位:账户区块。</summary>
    public IReadOnlyList<SettingsEntry> Account { get; } =
    [
        new("登录状态", "未登录"),
        new("API 地址", "https://api.lolia.link/api/v1"),
        new("Token 来源", "—")
    ];

    /// <summary>占位:外观区块。</summary>
    public IReadOnlyList<SettingsEntry> Appearance { get; } =
    [
        new("主题", "跟随系统"),
        new("语言", "简体中文")
    ];

    /// <summary>占位:frpc 核心区块。</summary>
    public IReadOnlyList<SettingsEntry> FrpcCore { get; } =
    [
        new("本地版本", "未安装"),
        new("下载源", "官方镜像")
    ];

    /// <summary>占位:关于区块。</summary>
    public IReadOnlyList<SettingsEntry> About { get; } =
    [
        new("客户端版本", "—"),
        new("最后检查更新", "从未")
    ];
}

/// <summary>设置页中的一行「标签 / 值」。</summary>
public sealed record SettingsEntry(string Label, string Value);
