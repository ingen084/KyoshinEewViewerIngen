using StringLiteral;
using System;
using Xunit;

namespace KyoshinEewViewer.JmaXmlParser.Tests
{
	public partial class JmaXmlDocumentTest
	{
		[Utf8("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<Report xmlns=\"http://xml.kishou.go.jp/jmaxml1/\" xmlns:jmx=\"http://xml.kishou.go.jp/jmaxml1/\"></Report>")]
		private static partial ReadOnlySpan<byte> CreateInstanceBody();

		[Fact]
		public void CreateInstance()
		{
			using var doc = new JmaXmlDocument(CreateInstanceBody());
			Assert.NotNull(doc);
		}

		[Utf8("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<Hoge></Hoge>")]
		private static partial ReadOnlySpan<byte> CreateInstanceErrorBody();

		[Fact]
		public void CreateInstanceError()
			=> Assert.Throws<JmaXmlParseException>(() =>
			{
				using var doc = new JmaXmlDocument(CreateInstanceErrorBody());
			});

		[Utf8("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<Report><Control></Control></Report>")]
		private static partial ReadOnlySpan<byte> GetControlNodeBody();

		[Fact]
		public void GetControlNode()
		{
			using var doc = new JmaXmlDocument(GetControlNodeBody());
			Assert.NotNull(doc.Control);
		}

		[Utf8("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<Report><Head></Head></Report>")]
		private static partial ReadOnlySpan<byte> GetHeadNodeBody();

		[Fact]
		public void GetHeadNode()
		{
			using var doc = new JmaXmlDocument(GetHeadNodeBody());
			Assert.NotNull(doc.Head);
		}
	}
}
