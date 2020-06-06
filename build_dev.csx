using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Web;

bool ExecuteAndCheckResult(string command, string args)
{
	var process = Process.Start(new ProcessStartInfo(command, args));
	process.WaitForExit();
	return process.ExitCode == 0;
}
void CopyDirectory(string src, string dst)
{
	if (!Directory.Exists(dst))
		Directory.CreateDirectory(dst);
	foreach(var file in Directory.GetFiles(src))
		File.Copy(file, Path.Combine(dst, Path.GetFileName(file)), true);
	foreach (var dir in Directory.GetDirectories(src))
		CopyDirectory(dir, Path.Combine(dst, new DirectoryInfo(dir).Name));
}

Console.WriteLine("ingen Easy BuildTool v3");

Console.WriteLine("Checking BuildTarget...");

if (!Directory.Exists(@"src\KyoshinEewViewer"))
{
	Console.WriteLine("Project folder not found.");
	Environment.Exit(1);
	return;
}

if (Directory.Exists(@"src\KyoshinEewViewer\bin\Debug\netcoreapp3.1\win10-x64\publish"))
{
	Console.WriteLine("Cleaning...");
	Directory.Delete(@"src\KyoshinEewViewer\bin\Debug\netcoreapp3.1\win10-x64\publish", true);
}
if (Directory.Exists(@"out"))
{
	Console.WriteLine("Cleaning...");
	Directory.Delete(@"out", true);
}

Console.WriteLine("Building...");
if (!ExecuteAndCheckResult("dotnet", $"publish src/KyoshinEewViewer/KyoshinEewViewer.csproj -r win10-x64 -c Debug"))
{
	Console.WriteLine("BuildFailed");
	Environment.Exit(1);
	return;
}
CopyDirectory(@"src\KyoshinEewViewer\bin\Debug\netcoreapp3.1\win10-x64\publish\", @"out");

Console.WriteLine("Build completed!");
