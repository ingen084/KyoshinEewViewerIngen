using SkiaSharp;
using System.IO;

namespace KyoshinEewViewer.CustomControl;

public partial class MapControl
{
	/// <summary>
	/// 現在の地図表示をラスター画像としてStreamに保存する
	/// </summary>
	/// <param name="stream">保存先のStream</param>
	/// <param name="format">画像フォーマット（デフォルトはPNG）</param>
	/// <param name="quality">JPEG/WEBPの品質（0-100）</param>
	/// <returns>保存が成功したかどうか</returns>
	public bool SaveToStream(Stream stream, SKEncodedImageFormat format = SKEncodedImageFormat.Png, int quality = 100)
	{
		if (Layers is null || PaddedRect.Width <= 0 || PaddedRect.Height <= 0)
			return false;

		var param = RenderParameter;
		var width = (int)param.PixelBound.Width;
		var height = (int)param.PixelBound.Height;

		if (width <= 0 || height <= 0)
			return false;

		using var recorder = new SKPictureRecorder();
		var bounds = new SKRect(0, 0, width, height);
		var canvas = recorder.BeginRecording(bounds);

		lock (LayerHost)
		{
			foreach (var layer in Layers)
				layer.Render(canvas, param, false);
		}

		using var picture = recorder.EndRecording();
		using var image = SKImage.FromPicture(picture, new SKSizeI(width, height));
		using var data = image.Encode(format, quality);

		if (data is null)
			return false;

		data.SaveTo(stream);
		return true;
	}

	/// <summary>
	/// 現在の地図表示をSKPicture（ベクター形式）としてStreamに保存する
	/// </summary>
	/// <param name="stream">保存先のStream</param>
	/// <returns>保存が成功したかどうか</returns>
	public bool SaveAsPictureToStream(Stream stream)
	{
		if (Layers is null || PaddedRect.Width <= 0 || PaddedRect.Height <= 0)
			return false;

		var param = RenderParameter;
		var width = (int)param.PixelBound.Width;
		var height = (int)param.PixelBound.Height;

		if (width <= 0 || height <= 0)
			return false;

		using var recorder = new SKPictureRecorder();
		var bounds = new SKRect(0, 0, width, height);
		var canvas = recorder.BeginRecording(bounds);

		lock (LayerHost)
		{
			foreach (var layer in Layers)
				layer.Render(canvas, param, false);
		}

		using var picture = recorder.EndRecording();
		picture.Serialize(stream);
		return true;
	}
}
