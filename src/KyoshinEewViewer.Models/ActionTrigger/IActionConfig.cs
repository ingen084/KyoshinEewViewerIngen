using System;

namespace KyoshinEewViewer.Models.ActionTrigger
{
	public interface IActionConfig
	{
		/// <summary>
		/// トリガーの識別子
		/// </summary>
		Guid HostIdentity { get; set; }
	}
}
