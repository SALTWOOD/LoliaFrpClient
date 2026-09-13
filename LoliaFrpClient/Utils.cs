using System;
using System.Runtime.InteropServices;
using System.Text;

namespace LoliaFrpClient;

public class Utils
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, StringBuilder packageFullName);

    public static bool IsPackaged()
    {
        var length = 0;
        var sb = new StringBuilder(0);

        var result = GetCurrentPackageFullName(ref length, sb);

        return result != 15700;
    }
}