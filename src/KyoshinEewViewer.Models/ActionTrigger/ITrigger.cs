namespace KyoshinEewViewer.Models.ActionTrigger
{
	public interface ITrigger<T> : IAction<T> where T : ITriggerConfig
	{
	}
}
