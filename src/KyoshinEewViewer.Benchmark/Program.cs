using Avalonia;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.ReactiveUI;
using Avalonia.Skia.Helpers;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using KyoshinEewViewer.Benchmark;
using KyoshinEewViewer.DCReportParser;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Data;
using SkiaSharp;
using System.Diagnostics;

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);

public abstract class AvaloniaBenchmarkBase
{
	public static AppBuilder BuildAvaloniaApp()
		=> AppBuilder.Configure<App>()
			// .UsePlatformDetect()
			.UseSkia()
			.UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
			.LogToTrace()
			.UseReactiveUI();

	public AvaloniaBenchmarkBase()
	{
		// Avalonia の初期化
		var builder = BuildAvaloniaApp();
		builder.SetupWithoutStarting();
	}
}

[MemoryDiagnoser]
public class RenderToImage : AvaloniaBenchmarkBase
{
	private MainWindow Window { get; set; }
	private SKBitmap Bitmap { get; set; }
	private SKCanvas Canvas { get; set; }
	[GlobalSetup]
	public void Setup()
	{
		Window = new();
		Window.Show();

		Bitmap = new SKBitmap((int)Window.ClientSize.Width, (int)Window.ClientSize.Height);
		Canvas = new SKCanvas(Bitmap);
	}

	[Benchmark]
	public byte[] RenderTargetBitmap()
	{
		using var stream = new MemoryStream();
		var pixelSize = new PixelSize((int)Window.ClientSize.Width, (int)Window.ClientSize.Height);
		var size = new Size(Window.ClientSize.Width, Window.ClientSize.Height);
		var dpiVector = new Vector(96, 96);
		using var renderBitmap = new RenderTargetBitmap(pixelSize, dpiVector);
		Window.Measure(size);
		Window.Arrange(new Rect(size));
		renderBitmap.Render(Window);
		renderBitmap.Save(stream);
		return stream.ToArray();
	}

	[Benchmark]
	public byte[] DrawingContextHelperWebp100()
	{
		var size = new Size(Window.ClientSize.Width, Window.ClientSize.Height);
		Window.Measure(size);
		Window.Arrange(new Rect(size));
		DrawingContextHelper.RenderAsync(Canvas, Window);

		using var stream = new MemoryStream();
		using (var data = Bitmap.Encode(SKEncodedImageFormat.Webp, 100))
			data.SaveTo(stream);
		return stream.ToArray();
	}

	[Benchmark]
	public byte[] DrawingContextHelperWebp50()
	{
		var size = new Size(Window.ClientSize.Width, Window.ClientSize.Height);
		Window.Measure(size);
		Window.Arrange(new Rect(size));
		DrawingContextHelper.RenderAsync(Canvas, Window);

		using var stream = new MemoryStream();
		using (var data = Bitmap.Encode(SKEncodedImageFormat.Webp, 50))
			data.SaveTo(stream);
		return stream.ToArray();
	}

	[Benchmark]
	public byte[] DrawingContextHelperPng()
	{
		var size = new Size(Window.ClientSize.Width, Window.ClientSize.Height);
		Window.Measure(size);
		Window.Arrange(new Rect(size));
		DrawingContextHelper.RenderAsync(Canvas, Window);

		using var stream = new MemoryStream();
		using (var data = Bitmap.Encode(SKEncodedImageFormat.Png, 100))
			data.SaveTo(stream);
		return stream.ToArray();
	}

	[GlobalCleanup]
	public void Dispose()
	{
		Bitmap?.Dispose();
		Canvas?.Dispose();
		Window?.Close();
	}
}

public class DCReportParseBenchmark
{
	public static IEnumerable<byte[]> GetSampleData()
	=> [
			Convert.FromHexString("9AAF8DED25000325BA00DA4A0F5AAC5A8000000008000000200000136DCCFB40"),
			Convert.FromHexString("9AAC89558B0003240000AB160F3A2499B40000000000002000000010C93712C0"),

			Convert.FromHexString("53AD16692E80035C0000D25A192E47C99E011B8000000000000000116D6E0A80"),
			Convert.FromHexString("53AD1466FA00035C0000CDF0052EC408000104000000000000000012D8017240"),
			Convert.FromHexString("53AD15BA49800351C5007412FFC7EE405E00FCC00000000000000012A54CEC80"),

			Convert.FromHexString("53AD1D312B800312B2300000000000000000000000000000000000124DBF4040"),
			Convert.FromHexString("C6AD1C66F980066F82B80000000000000000000000000000000000129A6F7B80"),

			Convert.FromHexString("9AAFADED200001E51A00524068480000000000000000000000000011C72342C0"),
			Convert.FromHexString("9AACA8BECF0001E8F67C37FF3348000000000000000000000000001316E6B240"),
			Convert.FromHexString("53ADA8BECF0001E8F67C27FF2C100000000000000000000000000012C1D36B40"),

			Convert.FromHexString("C6AD34AC278002DF0011700000000000000000000000000000000010B4FF8D80"),
			Convert.FromHexString("9AAD34C5CF000436A00C8000000000000000000000000000000000122AA54E80"),

			Convert.FromHexString("9AADC089B400009B40C22789F4691A17A00000000000000000000010FC738640"),
			Convert.FromHexString("53ADC51C200001C200B13B2BE3F00000000000000000000000000012270C7440"),
			Convert.FromHexString("C6AD424130000412E341F783E0F10910421230200000000000000013417E39C0"),

			Convert.FromHexString("53AD4894CC80014C043EE4C1F07826122081309181098496F8000012AAAFCB40"),
			Convert.FromHexString("53AD4DF194000718F23F46467F44323422C1519FD1098CFE8800001223D79E00"),

			Convert.FromHexString("C6AD54C59400011F05442F0544370544000000000000000000000012772C6FC0"),
			Convert.FromHexString("53AD5461988001AAE630AE1A80BAE630000000000000000000000012BA8FD040"),
			Convert.FromHexString("C6ADD465878002BB3450BBA98000000000000000000000000000001321D21040"),
			Convert.FromHexString("9AAD5608AC8001B1D4C0B1FBDAB222E0B35B60000000000000000011126BCC00"),

			Convert.FromHexString("53AD5D3594000260A8FCD8800000000000000000000000000000001256CC1680"),
			Convert.FromHexString("9AADDD34A78000E0A8F548908E0A8F54DD60E0A8F584EB000000001287160400"),
			Convert.FromHexString("9AAD5D3594000161D5F60D939E0A8F52B1320826FF3DB2000000001240A74700"),

			Convert.FromHexString("C6ADE465F900065E0100002800AD90138A00F80CA30000000000001185B5F7C0"),
			Convert.FromHexString("53ADE5BC768003DE030018640855E011C500F016C100000000000011AC6DC940"),
			Convert.FromHexString("53ADE666568006A4030030740837700E9900F156BC000000000000103A114FC0"),

			Convert.FromHexString("C6ADF465D18002C3F2587F8B101962082C41A588ACB1181623500012199FB000"),
			Convert.FromHexString("9AADF67DCA000013EC00000000000000000000000000000000000011B9CEED40"),
			Convert.FromHexString("53ADF679142002CFAAA1F7D53EAAA7DA0000000000000000000000139EF08580"),
	];

	[ParamsSource(nameof(GetSampleData))]
	public byte[] Hex { get; set; }

	[Benchmark]
	public DCReport Parse()
		=> DCReport.Parse(Hex);
}
