using System;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Models.ActionTrigger
{
	public interface IAction
	{
		/// <summary>
		/// 識別子
		/// </summary>
		Guid Identity { get; }
		/// <summary>
		/// 表示名
		/// </summary>
		string DisplayName { get; }

		/// <summary>
		/// アクションを実行する
		/// </summary>
		/// <param name="config"></param>
		/// <param name="parameter"></param>
		/// <returns></returns>
		ValueTask Execute(IActionConfig config, dynamic parameter);

		/// <summary>
		/// 設定内容の概要文字列を出力
		/// </summary>
		/// <param name="config">設定</param>
		/// <returns>概要</returns>
		string GetConfigSummaryString(IActionConfig config);
	}
}
