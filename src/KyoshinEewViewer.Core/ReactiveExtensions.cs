using Microsoft.Extensions.Logging;
using System;
using System.ComponentModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

namespace KyoshinEewViewer.Core;

public static class ReactiveExtensions
{
	/// <summary>
	/// プロパティの変更を監視する
	/// </summary>
	/// <param name="value">監視対象</param>
	/// <param name="propertySelector">監視するプロパティ (<c>x => x.Foo</c> の形で指定する)</param>
	/// <param name="pushCurrentValueOnSubscribe">購読時に現在の値を流すか</param>
	public static IObservable<TProperty> ObservePropertyChanged<T, TProperty>(
		this T value,
		Func<T, TProperty> propertySelector,
		bool pushCurrentValueOnSubscribe = true,
		[CallerArgumentExpression(nameof(propertySelector))] string? expr = null)
		where T : INotifyPropertyChanged
	{
		ArgumentNullException.ThrowIfNull(expr);
		return ObserveProperty(value, propertySelector, ToPropertyName(expr), pushCurrentValueOnSubscribe);
	}

	/// <summary>
	/// 入れ子になったプロパティの変更を監視する
	/// </summary>
	/// <remarks>途中のプロパティが差し替えられた場合は新しいオブジェクトを監視し直す。途中が null の間は値を流さない</remarks>
	public static IObservable<TProperty2> ObservePropertyChanged<T, TProperty1, TProperty2>(
		this T value,
		Func<T, TProperty1?> propertySelector1,
		Func<TProperty1, TProperty2> propertySelector2,
		bool pushCurrentValueOnSubscribe = true,
		[CallerArgumentExpression(nameof(propertySelector1))] string? propertySelector1Expr = null,
		[CallerArgumentExpression(nameof(propertySelector2))] string? propertySelector2Expr = null)
		where T : INotifyPropertyChanged
		where TProperty1 : INotifyPropertyChanged
	{
		ArgumentNullException.ThrowIfNull(propertySelector1Expr);
		ArgumentNullException.ThrowIfNull(propertySelector2Expr);
		var property1Name = ToPropertyName(propertySelector1Expr);
		var property2Name = ToPropertyName(propertySelector2Expr);
		return ObserveProperty(value, propertySelector1, property1Name, true)
			.Select(first => first is null
				? Observable.Empty<TProperty2>()
				: ObserveProperty(first, propertySelector2, property2Name, pushCurrentValueOnSubscribe))
			.Switch();
	}

	/// <inheritdoc cref="ObservePropertyChanged{T, TProperty1, TProperty2}"/>
	public static IObservable<TProperty3> ObservePropertyChanged<T, TProperty1, TProperty2, TProperty3>(
		this T value,
		Func<T, TProperty1?> propertySelector1,
		Func<TProperty1, TProperty2?> propertySelector2,
		Func<TProperty2, TProperty3> propertySelector3,
		bool pushCurrentValueOnSubscribe = true,
		[CallerArgumentExpression(nameof(propertySelector1))] string? propertySelector1Expr = null,
		[CallerArgumentExpression(nameof(propertySelector2))] string? propertySelector2Expr = null,
		[CallerArgumentExpression(nameof(propertySelector3))] string? propertySelector3Expr = null)
		where T : INotifyPropertyChanged
		where TProperty1 : INotifyPropertyChanged
		where TProperty2 : INotifyPropertyChanged
	{
		ArgumentNullException.ThrowIfNull(propertySelector1Expr);
		ArgumentNullException.ThrowIfNull(propertySelector2Expr);
		ArgumentNullException.ThrowIfNull(propertySelector3Expr);
		var property1Name = ToPropertyName(propertySelector1Expr);
		var property2Name = ToPropertyName(propertySelector2Expr);
		var property3Name = ToPropertyName(propertySelector3Expr);
		return ObserveProperty(value, propertySelector1, property1Name, true)
			.Select(first => first is null
				? Observable.Empty<TProperty3>()
				: ObserveProperty(first, propertySelector2, property2Name, true)
					.Select(second => second is null
						? Observable.Empty<TProperty3>()
						: ObserveProperty(second, propertySelector3, property3Name, pushCurrentValueOnSubscribe))
					.Switch())
			.Switch();
	}

	private static string ToPropertyName(string expr)
		=> expr[(expr.LastIndexOf('.') + 1)..];

	private static IObservable<TProperty> ObserveProperty<T, TProperty>(T value, Func<T, TProperty> propertySelector, string propertyName, bool pushCurrentValueOnSubscribe)
		where T : INotifyPropertyChanged
		=> Observable.Create<TProperty>(observer =>
		{
			if (pushCurrentValueOnSubscribe)
				observer.OnNext(propertySelector(value));

			void Handler(object? sender, PropertyChangedEventArgs e)
			{
				if (e.PropertyName == propertyName)
					observer.OnNext(propertySelector(value));
			}
			value.PropertyChanged += Handler;
			return () => value.PropertyChanged -= Handler;
		});

	/// <summary>
	/// 値を捨て、通知があったことだけを流す
	/// </summary>
	public static IObservable<Unit> AsUnitObservable<T>(this IObservable<T> source)
		=> source.Select(static _ => Unit.Default);

	/// <summary>
	/// 最初の値から <paramref name="interval"/> が経過した時点で、その間に届いた最後の値を流す
	/// </summary>
	/// <remarks>値が届いていない間はタイマーを動かさない</remarks>
	public static IObservable<T> ThrottleLast<T>(this IObservable<T> source, TimeSpan interval, TimeProvider? timeProvider = null)
		=> Observable.Create<T>(observer =>
		{
			var gate = new Lock();
			var hasValue = false;
			T? lastValue = default;

			var timer = (timeProvider ?? TimeProvider.System).CreateTimer(_ =>
			{
				lock (gate)
				{
					if (!hasValue)
						return;
					var value = lastValue!;
					hasValue = false;
					lastValue = default;
					observer.OnNext(value);
				}
			}, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

			var subscription = source.Subscribe(
				value =>
				{
					lock (gate)
					{
						if (!hasValue)
							timer.Change(interval, Timeout.InfiniteTimeSpan);
						hasValue = true;
						lastValue = value;
					}
				},
				observer.OnError,
				observer.OnCompleted);

			return () =>
			{
				subscription.Dispose();
				timer.Dispose();
			};
		});

	/// <summary>
	/// 購読する。<paramref name="onNext"/> が例外を投げてもログに記録して購読を続ける
	/// </summary>
	/// <remarks>
	/// 標準の <c>Subscribe</c> は、ハンドラーの例外を値の発行元 (プロパティを代入した側など) へ伝播させ、購読も解除する。
	/// 1箇所の例外で以後の更新が止まらないようにしたい購読にはこちらを使う
	/// </remarks>
	public static IDisposable SubscribeLoggingErrors<T>(this IObservable<T> source, Action<T> onNext)
		=> source.Subscribe(new ErrorLoggingObserver<T>(onNext));

	private sealed class ErrorLoggingObserver<T>(Action<T> onNext) : IObserver<T>
	{
		public void OnNext(T value)
		{
			try
			{
				onNext(value);
			}
			catch (Exception ex)
			{
				AppLog.Default.LogWarning(ex, "Rx の購読処理内で例外が発生しました");
			}
		}

		public void OnError(Exception error)
			=> AppLog.Default.LogWarning(error, "Rx の購読元がエラーで終了しました");

		public void OnCompleted()
		{
		}
	}
}
