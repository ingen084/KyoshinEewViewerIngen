using KyoshinEewViewer.JmaXmlParser;

var path = Console.ReadLine();
if (path == null || !File.Exists(path))
{
	Console.WriteLine("ファイルが見つかりません");
	return;
}

using var file = File.OpenRead(path);
using var document = new JmaXmlDocument(file);

;
