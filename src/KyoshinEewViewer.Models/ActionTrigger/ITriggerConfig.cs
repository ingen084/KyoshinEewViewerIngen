using System.Collections.Generic;

namespace KyoshinEewViewer.Models.ActionTrigger
{
	public interface ITriggerConfig : IActionConfig
	{
		IEnumerable<IActionConfig> ExecuteActions { get; set; }
	}
}
