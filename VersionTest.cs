using KyoshinEewViewer.Core;
using System;

public static class VersionTest
{
    public static void Main()
    {
        Console.WriteLine("バージョンパース機能のテスト:");
        Console.WriteLine();

        // テストケース
        var testCases = new[]
        {
            "1.0.0",
            "1.0.0-alpha", 
            "1.0.0-beta",
            "1.0.0-rc",
            "DEBUG",
            "EXP-123",
            "?",
            "2.1.0",
            "2.1.0-alpha"
        };

        foreach (var testCase in testCases)
        {
            var (version, suffix) = Utils.ParseVersionString(testCase);
            var priority = Utils.GetSuffixPriority(suffix);
            Console.WriteLine($"{testCase,-12} => Version: {version}, Suffix: {suffix ?? "null"}, Priority: {priority}");
        }

        Console.WriteLine();
        Console.WriteLine("バージョン比較テスト:");
        
        // DEBUGと1.0.0の比較
        var (debugVer, debugSuffix) = Utils.ParseVersionString("DEBUG");
        var (releaseVer, releaseSuffix) = Utils.ParseVersionString("1.0.0");
        
        var isDebugNewer = debugVer > releaseVer || 
                          (debugVer == releaseVer && Utils.IsNewerSuffix(releaseSuffix, debugSuffix));
        var isReleaseNewer = releaseVer > debugVer || 
                           (releaseVer == debugVer && Utils.IsNewerSuffix(debugSuffix, releaseSuffix));
        
        Console.WriteLine($"DEBUG vs 1.0.0:");
        Console.WriteLine($"  DEBUG is newer: {isDebugNewer}");
        Console.WriteLine($"  1.0.0 is newer: {isReleaseNewer}");
    }
}