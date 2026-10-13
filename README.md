<div align="center">

![LoliaFrpClient](https://socialify.git.ci/SALTWOOD/LoliaFrpClient/image?description=1&font=Inter&forks=1&issues=1&language=1&name=1&owner=1&pattern=Plus&pulls=1&stargazers=1&theme=Auto)

# LoliaFrpClient

LoliaFrp 内网穿透服务的跨平台客户端

[![License](https://img.shields.io/github/license/SALTWOOD/LoliaFrpClient)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![Avalonia](https://img.shields.io/badge/Avalonia-12-8B44AC)](https://avaloniaui.net/)

</div>

## 功能特性

- **总览与签到** - 查看账号信息、可用流量、隧道数量与每日流量趋势图
- **隧道管理** - 搜索、筛选、创建和删除隧道
- **节点列表** - 查看节点在线状态、负载与计费倍率，创建隧道
- **Frpc 管理** - 自动下载、安装和更新 frpc 核心
- **运行状态与日志** - 统一管理 frpc 进程，按隧道分标签页查看实时日志

## 技术栈

- **框架**: [.NET 10](https://dotnet.microsoft.com/) with [Avalonia](https://avaloniaui.net/) 12 + [FluentAvaloniaUI](https://github.com/amwx/FluentAvalonia)
- **API 客户端**: [Microsoft Kiota](https://learn.microsoft.com/en-us/openapi/kiota/)
- **平台**: Windows (x64 / arm64)、Linux (x64)、Android (arm64-v8a)

## 快速开始

### 从源码构建

1. **克隆仓库**
   ```bash
   git clone https://github.com/SALTWOOD/LoliaFrpClient.git
   cd LoliaFrpClient
   ```

2. **安装依赖**

   确保已安装 [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

3. **构建项目**
   ```bash
   dotnet restore
   dotnet build
   ```

4. **运行项目**
   ```bash
   dotnet run --project LoliaFrpClient.Desktop
   ```

### 构建 Android 端

需要 JDK 和 Android workload，frpc 由 CI 从上游源码交叉编译后打进 APK：

```bash
dotnet workload install android
dotnet build LoliaFrpClient.Android -c Release -f net10.0-android -p:RuntimeIdentifier=android-arm64
```

### 发布版本

下载最新的发布版本见 [Releases](https://github.com/SALTWOOD/LoliaFrpClient/releases) 页面。

## 使用说明

1. **登录账户** - 在设置页面使用 OAuth 登录您的 LoliaFrp 账户
2. **安装 Frpc** - 首次使用需要在设置页面安装 frpc 核心，Android 端已内置
3. **创建隧道** - 在节点页面选择节点并填写隧道信息
4. **启动隧道** - 在 Frpc 页面启动隧道，并在此查看运行日志

## 致谢

- [LoliaFRP](https://github.com/Lolia-FRP) - 内网穿透服务
