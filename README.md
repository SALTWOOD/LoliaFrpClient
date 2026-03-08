<div align="center">

![LoliaFrpClient](https://socialify.git.ci/SALTWOOD/LoliaFrpClient/image?description=1&font=Inter&forks=1&issues=1&language=1&name=1&owner=1&pattern=Plus&pulls=1&stargazers=1&theme=Auto)

# LoliaFrpClient

LoliaFrp 内网穿透服务的 Windows 桌面客户端

[![License](https://img.shields.io/github/license/SALTWOOD/LoliaFrpClient)](LICENSE.txt)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
[![WinUI 3](https://img.shields.io/badge/WinUI-3-005FB8)](https://microsoft.github.io/microsoft-ui-xaml/)

</div>

## 功能特性

- **隧道管理** - 查看、创建、启动和停止隧道
- **Frpc 管理** - 自动下载、安装和更新 frpc 核心
- **流量统计** - 实时查看流量使用情况和每日趋势图

## 技术栈

- **框架**: [.NET 8.0](https://dotnet.microsoft.com/) with WinUI 3
- **API 客户端**: [Microsoft Kiota](https://learn.microsoft.com/en-us/openapi/kiota/)

## 快速开始

### 从源码构建

1. **克隆仓库**
   ```bash
   git clone https://github.com/SALTWOOD/LoliaFrpClient.git
   cd LoliaFrpClient
   ```

2. **安装依赖**
   
   确保已安装 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) 和 [Visual Studio 2022](https://visualstudio.microsoft.com/)

3. **构建项目**
   ```bash
   dotnet restore
   dotnet build
   ```

4. **运行项目**
   ```bash
   dotnet run --project LoliaFrpClient
   ```

### 发布版本

下载最新的发布版本 from [Releases](https://github.com/SALTWOOD/LoliaFrpClient/releases) 页面。

## 使用说明

1. **登录账户** - 在设置页面使用 OAuth 登录您的 LoliaFrp 账户
2. **安装 Frpc** - 首次使用需要在设置页面安装 frpc 核心
3. **管理隧道** - 在隧道列表页面查看和管理您的隧道
4. **启动隧道** - 选择隧道并点击启动按钮开始内网穿透

## 致谢

- [LoliaFrp](https://github.com/LoliaFrp) - 内网穿透服务
