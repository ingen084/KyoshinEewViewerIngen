using System;
using Xunit;

namespace KyoshinEewViewer.JmaXmlParser.Tests
{
	public partial class JmaXmlDocumentTest
	{
		private static ReadOnlySpan<byte> CreateInstanceBody() => "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<Report xmlns=\"http://xml.kishou.go.jp/jmaxml1/\" xmlns:jmx=\"http://xml.kishou.go.jp/jmaxml1/\"></Report>"u8;

		[Fact]
		public void CreateInstance()
		{
			var doc = JmaXml.Read<TestView>(CreateInstanceBody());
			Assert.NotNull(doc);
		}

		private static ReadOnlySpan<byte> CreateInstanceErrorBody() => "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<Hoge></Hoge>"u8;

		[Fact]
		public void CreateInstanceError()
			=> Assert.Throws<JmaXmlParseException>(() =>
			{
				var doc = JmaXml.Read<TestView>(CreateInstanceErrorBody());
			});

		private static ReadOnlySpan<byte> GetControlNodeBody() => "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<Report><Control></Control></Report>"u8;

		[Fact]
		public void GetControlNode()
		{
			var doc = JmaXml.Read<TestView>(GetControlNodeBody());
			Assert.NotNull(doc.Control);
		}

		private static ReadOnlySpan<byte> GetHeadNodeBody() => "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<Report><Head></Head></Report>"u8;

		[Fact]
		public void GetHeadNode()
		{
			var doc = JmaXml.Read<TestView>(GetHeadNodeBody());
			Assert.NotNull(doc.Head);
		}
	}
}
