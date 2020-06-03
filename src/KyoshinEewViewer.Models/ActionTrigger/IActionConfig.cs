using System;

namespace KyoshinEewViewer.Models.ActionTrigger
{
	public interface IActionConfig
	{
		/// <summary>
		/// アクションの識別子
		/// </summary>
		Guid HostIdentity { get; set; }

		/// <summary>
		/// 次に実行するアクション
		/// 複数個入れた場合同時に実行しようとする
		/// </summary>
		IActionConfig[] NextActions { get; set; }
	}
}
