using KyoshinEewViewer.JmaXmlParser;

var path = Console.ReadLine();
if (path == null || !File.Exists(path))
{
	Console.WriteLine("ファイルが見つかりません");
	return;
}

using var file = File.OpenRead(path);
var document = JmaXml.Read<SandboxView>(file);

;

[JmaXmlView]
partial struct SandboxView;
