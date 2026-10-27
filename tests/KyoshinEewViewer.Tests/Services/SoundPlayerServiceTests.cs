using KyoshinEewViewer.Services;

namespace KyoshinEewViewer.Tests.Services;

public class SoundPlayerServiceTests
{
	[Fact(DisplayName = "保存した名前とドライバー名から再生デバイスを探せる")]
	public void FindDevice()
	{
		AudioDevice[] devices = [
			new(2, "USB Audio", "hw:0,0"),
			new(3, "vc4-hdmi-0", "hw:1,0"),
			new(4, "vc4-hdmi-0", "hw:2,0"),
		];

		// 名前とドライバー名の両方が一致するものを、名前だけが一致するものより優先する
		Assert.Equal(4, SoundPlayerService.FindDevice(devices, "vc4-hdmi-0", "hw:2,0")?.Index);
		// カード番号が変わってドライバー名が一致しなくなった場合は、名前で探す
		Assert.Equal(3, SoundPlayerService.FindDevice(devices, "vc4-hdmi-0", "hw:5,0")?.Index);
		// ドライバー名だけが一致する別のデバイスは選ばない
		Assert.Null(SoundPlayerService.FindDevice(devices, "Speaker", "hw:0,0"));
	}
}
