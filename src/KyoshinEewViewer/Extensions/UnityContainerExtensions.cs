using Unity;

namespace KyoshinEewViewer.Extensions
{
	public static class UnityContainerExtensions
	{
		public static T RegisterInstanceAndResolve<T>(this IUnityContainer container)
		{
			container.RegisterSingleton<T>();
			return container.Resolve<T>();
		}
	}
}
