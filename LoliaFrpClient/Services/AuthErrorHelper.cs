using System;
using System.Net;
using Microsoft.Kiota.Abstractions;

namespace LoliaFrpClient.Services;

internal static class AuthErrorHelper
{
    public static bool ShouldSilence(Exception exception)
    {
        if (string.IsNullOrWhiteSpace(SettingsStorage.Instance.OAuthToken))
        {
            return true;
        }

        return IsUnauthorized(exception);
    }

    private static bool IsUnauthorized(Exception? exception)
    {
        for (var current = exception; current != null; current = current.InnerException)
        {
            if (current is ApiException apiException && apiException.ResponseStatusCode == (int)HttpStatusCode.Unauthorized)
            {
                return true;
            }
        }

        return false;
    }
}
