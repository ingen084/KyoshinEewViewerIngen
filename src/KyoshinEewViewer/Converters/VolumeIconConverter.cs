using Avalonia.Data.Converters;
using FluentAvalonia.UI.Controls;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace KyoshinEewViewer.Converters;

/// <summary>
/// ミュート状態と音量から表示するスピーカーアイコンを決定するコンバーター。
/// values[0]: bool (ミュート中か), values[1]: double (音量 0〜1)
/// </summary>
public class VolumeIconConverter : IMultiValueConverter
{
	/// <summary>
	/// 小音量と大音量のアイコンを切り替えるしきい値
	/// </summary>
	private const double LoudVolumeThreshold = .5;

	public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
	{
		if (values.Count > 0 && values[0] is true)
			return FASymbol.SpeakerOff;

		var volume = values.Count > 1 && values[1] is double v ? v : 0;
		if (volume <= 0)
			return FASymbol.Speaker0;
		return volume <= LoudVolumeThreshold ? FASymbol.Speaker1 : FASymbol.Speaker2;
	}
}
