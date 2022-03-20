using KyoshinEewViewer.JmaXmlParser.Data;
using System;
using System.IO;
using U8Xml;

namespace KyoshinEewViewer.JmaXmlParser
{
	public class JmaXmlDocument : IDisposable
	{
		public bool Disposed { get; private set; }

		private XmlObject Xml { get; }

		/// <summary>
		/// JmaXmlDocumentを初期化する
		/// </summary>
		/// <param name="body">電文</param>
		/// <exception cref="FormatException">XMLのフォーマットが正しくない</exception>
		/// <exception cref="JmaXmlParseException">ルートノードが Report ではない</exception>
		public JmaXmlDocument(Stream body)
		{
			Xml = XmlParser.Parse(body);
			if (Xml.Root.Name != Literals.Report())
				throw new JmaXmlParseException("ルートノードが Report ではありません");
		}
		/// <summary>
		/// JmaXmlDocumentを初期化する
		/// </summary>
		/// <param name="body">電文</param>
		/// <exception cref="FormatException">XMLのフォーマットが正しくない</exception>
		/// <exception cref="JmaXmlParseException">ルートノードが Report ではない</exception>
		public JmaXmlDocument(ReadOnlySpan<byte> body)
		{
			Xml = XmlParser.Parse(body);
			if (Xml.Root.Name != Literals.Report())
				throw new JmaXmlParseException("ルートノードが Report ではありません");
		}

		~JmaXmlDocument()
		{
			Dispose(false);
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (Disposed)
				return;
			Xml.Dispose();
			Disposed = true;
		}

		private ControlMeta? controlMeta;
		/// <summary>
		/// 電文の管理部を取得する
		/// </summary>
		/// <returns>Control メタデータクラス</returns>
		/// <exception cref="JmaXmlParseException">ノードが見つからなかった場合など</exception>
		public ControlMeta Control
		{
			get {
				if (controlMeta != null)
					return controlMeta;
				if (!Xml.Root.TryFindChild(Literals.Control(), out var node))
					throw new JmaXmlParseException("Control ノードが見つかりません");
				return controlMeta = new ControlMeta(node);
			}
		}

		private HeadData? headData;
		/// <summary>
		/// 電文のヘッダ部を取得する
		/// </summary>
		/// <returns>HeadData データクラス</returns>
		/// <exception cref="JmaXmlParseException">ノードが見つからなかった場合など</exception>
		public HeadData Head
		{
			get {
				if (headData != null)
					return headData;
				if (!Xml.Root.TryFindChild(Literals.Head(), out var node))
					throw new JmaXmlParseException("HeadData ノードが見つかりません");
				return headData = new HeadData(node);
			}
		}
	}
}
