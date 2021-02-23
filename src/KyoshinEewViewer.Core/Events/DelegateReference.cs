using System;
using System.Reflection;

namespace KyoshinEewViewer.Core.Events
{
	/// <summary>
	/// delegate/WeakReferenceなメソッドへの参照を示す
	/// </summary>
	public class DelegateReference
	{
		// use deleagte
		private readonly Delegate _delegate;
		// not use delegate
		private readonly WeakReference _weakReference;
		private readonly MethodInfo _method;
		private readonly Type _delegateType;

#pragma warning disable CS8618 // null 非許容のフィールドには、コンストラクターの終了時に null 以外の値が入っていなければなりません。Null 許容として宣言することをご検討ください。
		public DelegateReference(Delegate @delegate, bool keepReferenceAlive)
#pragma warning restore CS8618 // null 非許容のフィールドには、コンストラクターの終了時に null 以外の値が入っていなければなりません。Null 許容として宣言することをご検討ください。
		{
			if (@delegate == null)
				throw new ArgumentNullException(nameof(@delegate));

			if (keepReferenceAlive)
			{
				_delegate = @delegate;
				return;
			}
			_weakReference = new WeakReference(@delegate.Target);
			_method = @delegate.GetMethodInfo();
			_delegateType = @delegate.GetType();
		}
		public Delegate? Target
		{
			get
			{
				if (_delegate != null)
					return _delegate;
				if (_method.IsStatic)
					return _method.CreateDelegate(_delegateType, null);
				if (_weakReference.Target is object target)
					return _method.CreateDelegate(_delegateType, target);
				return null;
			}
		}
		public bool TargetEquals(Delegate @delegate)
		{
			if (_delegate != null)
				return _delegate == @delegate;
			if (@delegate == null)
				return !_method.IsStatic && !_weakReference.IsAlive;
			return _weakReference.Target == @delegate.Target && Equals(_method, @delegate.GetMethodInfo());
		}
	}
}
