using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using LoliaFrpClient.ViewModels;
using LoliaFrpClient.Views;

namespace LoliaFrpClient;

/// <summary>
/// 按 ViewModel 类型解析出对应的视图。
/// </summary>
/// <remarks>
///     刻意不采用「把 ViewModel 全名里的 ViewModel 换成 View 再反射查找」的约定式实现:
///     那种写法依赖 <c>Type.GetType</c> 与 <c>Activator.CreateInstance</c>,在 NativeAOT /
///     裁剪下会触发 IL2026,且失效方式是静默的——找不到视图就退化成一段文字,不抛异常。
///     改为显式登记,新增页面时在 <see cref="Views" /> 里加一行。
/// </remarks>
public class ViewLocator : IDataTemplate
{
    private static readonly Dictionary<Type, Func<Control>> Views = new()
    {
        [typeof(UserInfoViewModel)] = static () => new UserInfoPage(),
        [typeof(TunnelListViewModel)] = static () => new TunnelListPage(),
        [typeof(FrpcManagerViewModel)] = static () => new FrpcManagerPage(),
        [typeof(SettingsViewModel)] = static () => new SettingsPage()
    };

    /// <inheritdoc />
    public Control? Build(object? param)
    {
        if (param is null)
        {
            return null;
        }

        var viewModelType = param.GetType();

        return Views.TryGetValue(viewModelType, out var factory)
            ? factory()
            : new TextBlock { Text = "Not Found: " + viewModelType.FullName };
    }

    /// <inheritdoc />
    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}
