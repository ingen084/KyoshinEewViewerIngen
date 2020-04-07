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

const string PROJECTS_ROOT = "src";

Console.WriteLine("ingen Easy BuildTool v2");

Console.WriteLine("Scanning BuildTarget...");

if (!Directory.Exists(@"src\KyoshinEewViewer"))
{
	Console.WriteLine("Project folder not found.");
	Environment.Exit(1);
	return;
}
if (!Directory.GetDirectories(@"C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\VC\Tools\MSVC\")?.Any() ?? true)
{
	Console.WriteLine("vc tools folder not found.");
	Environment.Exit(1);
	return;
}

if (Directory.Exists(@"src\KyoshinEewViewer\bin\Release\netcoreapp3.1\win10-x64\publish"))
{
	Console.WriteLine("Cleaning...");
	foreach (var f in Directory.GetFiles(@"src\KyoshinEewViewer\bin\release\netcoreapp3.1\win10-x64\publish"))
		File.Delete(f);
	Directory.Delete(@"src\KyoshinEewViewer\bin\release\netcoreapp3.1\win10-x64\publish");
}

Console.WriteLine("Building...");
if (!ExecuteAndCheckResult("dotnet", $"publish src/KyoshinEewViewer/KyoshinEewViewer.csproj -r win10-x64 -c Release"))
{
	Console.WriteLine("BuildFailed");
	Environment.Exit(1);
	return;
}
foreach (var f in Directory.GetFiles(@"src\KyoshinEewViewer\bin\Release\netcoreapp3.1\win10-x64\publish", "*.pdb"))
	File.Delete(f);

Console.WriteLine("Packing...");
if (!File.Exists("tmp/warp-packer.exe"))
{
	if (!Directory.Exists("tmp"))
		Directory.CreateDirectory("tmp");
	ExecuteAndCheckResult("powershell", $"Invoke-WebRequest https://github.com/dgiagio/warp/releases/download/v0.3.0/windows-x64.warp-packer.exe -OutFile tmp/warp-packer.exe");
}
if (!ExecuteAndCheckResult("tmp/warp-packer.exe", $"--arch windows-x64 --input_dir src/KyoshinEewViewer/bin/Release/netcoreapp3.1/win10-x64/publish --exec KyoshinEewViewer.exe --output tmp/KyoshinEewViewer.exe"))
{
	Console.WriteLine("Warp Failed");
	Environment.Exit(1);
	return;
}

if (!File.Exists("tmp/ResourceHacker/ResourceHacker.exe"))
{
	ExecuteAndCheckResult("powershell", $"Invoke-WebRequest http://www.angusj.com/resourcehacker/resource_hacker.zip -OutFile tmp/resource_hacker.zip");
	ExecuteAndCheckResult("powershell", $"Expand-Archive -Path tmp/resource_hacker.zip -DestinationPath tmp/ResourceHacker");
}
if (!ExecuteAndCheckResult("tmp/ResourceHacker/ResourceHacker.exe", $"-open tmp/KyoshinEewViewer.exe -save tmp/KyoshinEewViewer.exe -action addskip -res src/KyoshinEewViewer/Resources/icon.ico -mask ICONGROUP,MAINICON,"))
{
	Console.WriteLine("ResourceHack Failed");
	Environment.Exit(1);
	return;
}

ExecuteAndCheckResult(Path.Combine(Directory.GetDirectories(@"C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\VC\Tools\MSVC\").First() ,@"bin\Hostx86\x86\editbin.exe"), "/subsystem:windows tmp/KyoshinEewViewer.exe");

Console.WriteLine("Build completed!");
