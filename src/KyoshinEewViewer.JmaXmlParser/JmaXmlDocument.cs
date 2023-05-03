using KyoshinEewViewer.JmaXmlParser.Data;
using KyoshinEewViewer.JmaXmlParser.Data.Earthquake;
using KyoshinEewViewer.JmaXmlParser.Data.Meteorological;
using KyoshinEewViewer.JmaXmlParser.Data.Tsunami;
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
			Xml?.Dispose();
			Disposed = true;
		}

		private ControlMeta? _controlMeta = null;
		/// <summary>
		/// 電文の管理部を取得する
		/// </summary>
		/// <returns>Control メタデータクラス</returns>
		/// <exception cref="JmaXmlParseException">ノードが見つからなかった場合など</exception>
		public ControlMeta Control
		{
			get {
				if (_controlMeta is ControlMeta meta)
					return meta;
				if (!Xml.Root.TryFindChild(Literals.Control(), out var node))
					throw new JmaXmlParseException("Control ノードが見つかりません");
				_controlMeta = new ControlMeta(node);
				return _controlMeta.Value;
			}
		}

		private HeadData? _headData = null;
		/// <summary>
		/// 電文のヘッダ部を取得する
		/// </summary>
		/// <returns>HeadData データクラス</returns>
		/// <exception cref="JmaXmlParseException">ノードが見つからなかった場合など</exception>
		public HeadData Head
		{
			get {
				if (_headData is HeadData head)
					return head;
				if (!Xml.Root.TryFindChild(Literals.Head(), out var node))
					throw new JmaXmlParseException("HeadData ノードが見つかりません");
				_headData = new HeadData(node);
				return _headData.Value;
			}
		}

		private EarthquakeBody? _earthquakeBody = null;
		/// <summary>
		/// 地震情報の電文の内容部を取得する
		/// </summary>
		/// <returns>EarthquakeBody データクラス</returns>
		/// <exception cref="JmaXmlParseException">ノードが見つからなかった場合など</exception>
		public EarthquakeBody EarthquakeBody
		{
			get {
				if (_earthquakeBody is EarthquakeBody body)
					return body;
				if (!Xml.Root.TryFindChild(Literals.Body(), out var node))
					throw new JmaXmlParseException("EarthquakeBody ノードが見つかりません");
				_earthquakeBody = new EarthquakeBody(node);
				return _earthquakeBody.Value;
			}
		}

		private TsunamiBody? _tsunamiBody = null;
		/// <summary>
		/// 津波情報の電文の内容部を取得する
		/// </summary>
		/// <returns>TsunamiBody データクラス</returns>
		/// <exception cref="JmaXmlParseException">ノードが見つからなかった場合など</exception>
		public TsunamiBody TsunamiBody
		{
			get {
				if (_tsunamiBody is TsunamiBody body)
					return body;
				if (!Xml.Root.TryFindChild(Literals.Body(), out var node))
					throw new JmaXmlParseException("TsunamiBody ノードが見つかりません");
				_tsunamiBody = new TsunamiBody(node);
				return _tsunamiBody.Value;
			}
		}

		private MeteorologicalBody? _meteorologicalBody = null;
		/// <summary>
		/// 気象情報の電文の内容部を取得する
		/// </summary>
		/// <returns>MeteorologicalBody データクラス</returns>
		/// <exception cref="JmaXmlParseException">ノードが見つからなかった場合など</exception>
		public MeteorologicalBody MeteorologicalBody
		{
			get {
				if (_meteorologicalBody is MeteorologicalBody body)
					return body;
				if (!Xml.Root.TryFindChild(Literals.Body(), out var node))
					throw new JmaXmlParseException("MeteorologicalBody ノードが見つかりません");
				_meteorologicalBody = new MeteorologicalBody(node);
				return _meteorologicalBody.Value;
			}
		}
	}
}
