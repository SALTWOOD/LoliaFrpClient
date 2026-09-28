using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using LoliaFrpClient.ViewModels;

namespace LoliaFrpClient;

/// <summary>
/// 按 ViewModel 类型解析出对应的视图。
/// </summary>
/// <remarks>
///     <para>
///         这里刻意不采用模板默认的「把 ViewModel 全名里的 <c>ViewModel</c> 替换成 <c>View</c>
///         再反射查找」的约定式实现。那种写法依赖 <see cref="Type.GetType(string)" /> 与
///         <c>Activator.CreateInstance</c>,在 NativeAOT / 裁剪下会触发 IL2026,
///         且失效方式是静默的——找不到视图就退化成一段文字,不抛异常。
///     </para>
///     <para>
///         改为在 <see cref="Views" /> 中显式登记。代价是新增视图要手动加一行,
///         换来的是编译期就能发现遗漏,且裁剪器能看见真实的类型引用。
///     </para>
/// </remarks>
public class ViewLocator : IDataTemplate
{
    private static readonly Dictionary<Type, Func<Control>> Views = new()
    {
        // 新增视图时在此登记,例如:
        // [typeof(TunnelListViewModel)] = static () => new TunnelListView(),
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
