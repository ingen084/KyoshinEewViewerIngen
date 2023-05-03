using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Rendering;
using KyoshinEewViewer.Core;
using System;
using System.Linq;
using System.Reflection;

namespace PiDASPlusGraph
{
    public class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow();
            }

            base.OnFrameworkInitializationCompleted();
        }

        public override void RegisterServices()
        {
            if (!Design.IsDesignMode)
            {
                var timer = AvaloniaLocator.CurrentMutable.GetService<IRenderTimer>() ?? throw new Exception("RenderTimer が取得できません");
                AvaloniaLocator.CurrentMutable.Bind<IRenderTimer>().ToConstant(new FrameSkippableRenderTimer(timer));
            }
            base.RegisterServices();
        }
    }

    public class FrameSkippableRenderTimer : IRenderTimer
    {
        public static uint SkipAmount { get; set; }

        private IRenderTimer ParentTimer { get; }
        private ulong FrameCount { get; set; }

        public void NotClientImplementable() => throw new NotImplementedException();

        public bool RunsInBackground => ParentTimer.RunsInBackground;

        public event Action<TimeSpan>? Tick;

        public FrameSkippableRenderTimer(IRenderTimer parentTimer)
        {
            ParentTimer = parentTimer;

            // ここに流れた時点ですでに RenderLoop のハンドラーが設定されているのでリフレクションで無理やり奪う
            var tickEvent = parentTimer.GetType().GetField("Tick", BindingFlags.Instance | BindingFlags.NonPublic);
            if (tickEvent?.GetValue(parentTimer) is MulticastDelegate handler)
            {
                foreach (var d in handler.GetInvocationList().Cast<Action<TimeSpan>>())
                {
                    ParentTimer.Tick -= d;
                    Tick += d;
                }
            }

            ParentTimer.Tick += t =>
            {
                if (SkipAmount <= 1 || FrameCount++ % SkipAmount == 0)
                    Tick?.Invoke(t);
            };
        }
    }
}
