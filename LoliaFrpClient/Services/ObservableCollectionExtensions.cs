using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace LoliaFrpClient.Services;

/// <summary>
///     <see cref="ObservableCollection{T}"/> 的批量操作辅助方法。
/// </summary>
public static class ObservableCollectionExtensions
{
    /// <summary>
    ///     用给定元素整体替换集合内容。
    ///     <para>
    ///         注意：<see cref="ObservableCollection{T}"/> 没有批量替换 API，
    ///         因此这里逐项 Add，会为每一项触发一次 CollectionChanged。
    ///         对于长列表，调用方应优先考虑直接替换整个集合对象。
    ///     </para>
    /// </summary>
    public static void ReplaceWith<T>(this ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items) target.Add(item);
    }
}
