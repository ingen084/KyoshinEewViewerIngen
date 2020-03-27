using System;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Models.ActionTrigger
{
	public interface IAction<T> where T : IActionConfig
	{
		/// <summary>
		/// 識別子
		/// </summary>
		Guid Identity { get; }
		string DisplayName { get; }

		ValueTask Execute(T config, dynamic parameter);

		/// <summary>
		/// 内容の概要文字列を出力
		/// </summary>
		/// <param name="config">設定</param>
		/// <returns>概要</returns>
		string ToSummaryString(T config);
	}
}
