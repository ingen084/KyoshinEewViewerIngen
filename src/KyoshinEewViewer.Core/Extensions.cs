using KyoshinMonitorLib;
using Splat;
using System;

namespace KyoshinEewViewer.Core;

public static class Extensions
{
	public static double Distance(this Location point1, Location point2)
		=> 6371 * Math.Acos(Math.Cos(point1.Latitude * Math.PI / 180) * Math.Cos(point2.Latitude * Math.PI / 180) * Math.Cos(point2.Longitude * Math.PI / 180 - point1.Longitude * Math.PI / 180) + Math.Sin(point1.Latitude * Math.PI / 180) * Math.Sin(point2.Latitude * Math.PI / 180));

	public static T RequireService<T>(this IReadonlyDependencyResolver resolver, string? contract = null)
	{
		if (resolver is null)
		{
			throw new ArgumentNullException(nameof(resolver));
		}

		return (T)(resolver.GetService(typeof(T), contract) ?? throw new InvalidOperationException($"Service \"{typeof(T)}\" is NotFound"));
	}

	public static void LogError(this ILogger logger, Exception exception, string message)
		=> logger.Write(exception, message, LogLevel.Error);
	public static void LogError(this ILogger logger, string message)
		=> logger.Write(message, LogLevel.Error);

	public static void LogWarning(this ILogger logger, Exception exception, string message)
		=> logger.Write(exception, message, LogLevel.Warn);
	public static void LogWarning(this ILogger logger, string message)
		=> logger.Write(message, LogLevel.Warn);

	public static void LogInfo(this ILogger logger, Exception exception, string message)
		=> logger.Write(exception, message, LogLevel.Info);
	public static void LogInfo(this ILogger logger, string message)
		=> logger.Write(message, LogLevel.Info);

	public static void LogDebug(this ILogger logger, Exception exception, string message)
		=> logger.Write(exception, message, LogLevel.Debug);
	public static void LogDebug(this ILogger logger, string message)
		=> logger.Write(message, LogLevel.Debug);
}
