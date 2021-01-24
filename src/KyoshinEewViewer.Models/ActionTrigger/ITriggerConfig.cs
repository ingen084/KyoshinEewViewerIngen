using System;

namespace KyoshinEewViewer.Models.ActionTrigger
{
    public interface ITriggerConfig
	{
		/// <summary>
		/// トリガーの識別子
		/// </summary>
		Guid HostIdentity { get; set; }

		/// <summary>
		/// 条件適合時実行するアクション
		/// 複数個入れた場合同時に実行しようとする
		/// </summary>
		IActionConfig[] ExecuteActions { get; set; }
	}
}
