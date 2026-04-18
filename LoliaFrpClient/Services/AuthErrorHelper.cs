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
            if (HasUnauthorizedStatusCode(current))
            {
                return true;
            }

            if (current is ApiException && current.Message.Contains("401", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasUnauthorizedStatusCode(Exception exception)
    {
        foreach (var propertyName in new[] { "ResponseStatusCode", "StatusCode" })
        {
            var property = exception.GetType().GetProperty(propertyName);
            if (property?.GetValue(exception) is HttpStatusCode httpStatusCode)
            {
                return httpStatusCode == HttpStatusCode.Unauthorized;
            }

            if (property?.GetValue(exception) is int numericStatusCode)
            {
                return numericStatusCode == (int)HttpStatusCode.Unauthorized;
            }

            if (property?.GetValue(exception) is string stringStatusCode && stringStatusCode == "401")
            {
                return true;
            }
        }

        return false;
    }
}
