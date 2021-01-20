using System;

namespace KyoshinEewViewerIngen.Dmdata
{
	/// <summary>
	/// 配信区分
	/// </summary>
	public enum TelegramCategory
	{
		/// <summary>
		/// 地震・津波関連
		/// </summary>
		Earthquake = 0,
		/// <summary>
		/// 火山関連
		/// </summary>
		Volcano,
		/// <summary>
		/// 気象警報･注意報関連
		/// </summary>
		Weather,
		/// <summary>
		/// 定時関連
		/// </summary>
		Scheduled,
	}

	public static class TelegramCategoryExtensions
	{
		/// <summary>
		/// パラメータで使用する形式に変換する
		/// </summary>
		/// <param name="cat">変換元</param>
		/// <returns></returns>
		public static string ToParameterString(this TelegramCategory cat)
			=> cat switch
			{
				TelegramCategory.Earthquake => "telegram.earthquake",
				TelegramCategory.Volcano => "telegram.volcano",
				TelegramCategory.Weather => "telegram.weather",
				TelegramCategory.Scheduled => "telegram.scheduled",
				_ => throw new ArgumentException("存在しないパラメータを変換しようとしました", nameof(cat)),
			};
	}
}
