using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using LoliaFrpClient.ViewModels;
using LoliaFrpClient.Views;

namespace LoliaFrpClient;

public class ViewLocator : IDataTemplate
{
    private static readonly Dictionary<Type, Func<Control>> Views = new()
    {
        [typeof(UserInfoViewModel)] = static () => new UserInfoPage(),
        [typeof(TunnelListViewModel)] = static () => new TunnelListPage(),
        [typeof(NodesPageViewModel)] = static () => new NodesPage(),
        [typeof(FrpcManagerViewModel)] = static () => new FrpcManagerPage(),
        [typeof(SettingsViewModel)] = static () => new SettingsPage()
    };

    public Control? Build(object? param)
    {
        if (param is null) return null;

        var viewModelType = param.GetType();

        return Views.TryGetValue(viewModelType, out var factory)
            ? factory()
            : new TextBlock { Text = "Not Found: " + viewModelType.FullName };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}