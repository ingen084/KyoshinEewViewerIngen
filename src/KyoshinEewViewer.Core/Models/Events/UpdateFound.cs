namespace KyoshinEewViewer.Core.Models.Events
{
	public class UpdateFound
	{
		public bool Found { get; }

		public UpdateFound(bool found)
		{
			Found = found;
		}
	}
}