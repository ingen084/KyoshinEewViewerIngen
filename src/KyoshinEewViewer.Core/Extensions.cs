using Avalonia.Platform;
using Avalonia.Skia;
using KyoshinMonitorLib;
using System;
using System.Linq.Expressions;
using System.Reflection;

namespace KyoshinEewViewer.Core;

public static class Extensions
{
	public static double Distance(this Location point1, Location point2)
		=> 6371 * Math.Acos(Math.Cos(point1.Latitude * Math.PI / 180) * Math.Cos(point2.Latitude * Math.PI / 180) * Math.Cos(point2.Longitude * Math.PI / 180 - point1.Longitude * Math.PI / 180) + Math.Sin(point1.Latitude * Math.PI / 180) * Math.Sin(point2.Latitude * Math.PI / 180));


	private static Func<object, object> BuildAccessor(FieldInfo field)
	{
		var obj = Expression.Parameter(typeof(object), "obj");
		var instance = field.IsStatic ? null : Expression.Convert(obj, field.DeclaringType);
		var expr = Expression.Lambda<Func<object, object>>(
		  Expression.Convert(
			Expression.Field(instance, field),
			typeof(object)),
		  obj);

		return expr.Compile();
	}

	private static Func<object, object>? GetInnerDrawingContextFunc;
	public static ISkiaDrawingContextImpl? TryGetSkiaDrawingContext(this IDrawingContextImpl context)
	{
		try
		{
			if (context is ISkiaDrawingContextImpl skiaImpl)
				return skiaImpl;
			if (GetInnerDrawingContextFunc == null)
			{
				var field = context.GetType().GetField("_impl", BindingFlags.InvokeMethod | BindingFlags.NonPublic | BindingFlags.Instance)
					?? throw new Exception("フィールドが存在しません");
				GetInnerDrawingContextFunc = BuildAccessor(field);
			}
			return GetInnerDrawingContextFunc(context) as ISkiaDrawingContextImpl;
		}
		catch
		{
			return null;
		}
	}
}
