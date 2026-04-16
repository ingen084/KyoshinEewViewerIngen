using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Styling;
using AvaloniaEdit.CodeCompletion;
using FluentAvalonia.UI.Controls;
using KyoshinEewViewer.Services;
using KyoshinEewViewer.Views.Components.TemplateEditor;
using Splat;

namespace KyoshinEewViewer.Views.Components;

public partial class TemplateEditorDialog : Window
{
    private bool _isResultOk;
    private CompletionWindow? _completionWindow;
    private Type? _eventType;

    public TemplateEditorDialog()
    {
        InitializeComponent();
        InitializeEditor();
    }

    public static Task<(bool success, string? templateText)> ShowAsync(Window parent, string title, string initialText, Type? eventType = null)
        => ShowInternalAsync(parent, title, initialText, eventType);

    public static Task<(bool success, string? templateText)> ShowAsync(string title, string initialText, Type? eventType = null)
    {
        var settingWindow = Locator.Current.GetService<ISubWindowsService>()?.SettingWindow;
        return ShowInternalAsync(settingWindow, title, initialText, eventType);
    }

    private static async Task<(bool success, string? templateText)> ShowInternalAsync(
        Window? parent, string title, string initialText, Type? eventType)
    {
        var viewModel = new TemplateEditorDialogViewModel
        {
            Title = title,
            TemplateText = initialText,
        };

        var dialog = new TemplateEditorDialog
        {
            DataContext = viewModel,
            _eventType = eventType,
        };

        dialog.Editor.Text = initialText;
        dialog.Editor.TextChanged += (_, _) => viewModel.TemplateText = dialog.Editor.Text;

        if (parent != null)
            await dialog.ShowDialog(parent);
        else
            dialog.Show();

        return dialog._isResultOk ? (true, viewModel.TemplateText) : (false, null);
    }

    private void InitializeEditor()
    {
        // 構文ハイライト (テーマに応じて配色を切り替え)
        var isDark = TryGetThemeBool("IsDarkTheme") ?? true;
        Editor.TextArea.TextView.LineTransformers.Add(new ScribanHighlightTransformer(isDark));

        // エディタの背景・前景もテーマに合わせる
        Editor.Background = GetThemeBrush("MainBackgroundColor", "#1E1E1E");
        Editor.Foreground = GetThemeBrush("ForegroundColor", "#F1F1F1");

        // オートコンプリート
        Editor.TextArea.TextEntering += OnTextEntering;
        Editor.TextArea.TextEntered += OnTextEntered;
    }

    /// <summary>
    /// アプリケーションリソースから Color を取得し、SolidColorBrush として返す
    /// </summary>
    private static IBrush GetThemeBrush(string resourceKey, string fallbackHex)
        => new SolidColorBrush(GetThemeColor(resourceKey, fallbackHex));

    /// <summary>
    /// アプリケーションリソースから Color を取得する
    /// </summary>
    private static Color GetThemeColor(string resourceKey, string fallbackHex)
    {
        if (KyoshinEewViewerApp.Application?.TryFindResource(resourceKey, out var value) == true)
        {
            if (value is Color color)
                return color;
            if (value is SolidColorBrush brush)
                return brush.Color;
        }
        return Color.Parse(fallbackHex);
    }

    private static bool? TryGetThemeBool(string resourceKey)
    {
        if (KyoshinEewViewerApp.Application?.TryFindResource(resourceKey, out var value) == true && value is bool b)
            return b;
        return null;
    }

    private void OnTextEntering(object? sender, TextInputEventArgs e)
    {
        try
        {
            if (_completionWindow == null || e.Text == null || e.Text.Length == 0)
                return;

            if (!char.IsLetterOrDigit(e.Text[0]) && e.Text[0] != '_' && e.Text[0] != '.')
                _completionWindow.CompletionList.RequestInsertion(e);
        }
        catch (Exception ex)
        {
            // 補完処理で例外が発生してもエディタの操作を妨げないように握り潰す
            LogHost.Default.Warn(ex, "コード補完の入力前処理で例外が発生しました");
            CloseCompletionWindowSafely();
        }
    }

    private void OnTextEntered(object? sender, TextInputEventArgs e)
    {
        try
        {
            if (e.Text == null || e.Text.Length == 0)
                return;

            var ch = e.Text[0];

            if (!ScribanCompletionData.IsInsideCodeBlock(Editor.Text, Editor.TextArea.Caret.Offset))
                return;

            if (ch == '.')
            {
                var objectName = GetWordBeforeDot(Editor.TextArea.Caret.Offset - 1);
                var members = ScribanCompletionData.GetMembersFor(objectName, Editor.Text, Editor.TextArea.Caret.Offset, _eventType);
                if (members.Count > 0)
                    ShowCompletionWindow(members, 0);
            }
            else if (ch == '"')
            {
                // 文字列リテラル開始時、enum 比較コンテキストなら enum 値を補完
                var enumValues = ScribanCompletionData.GetEnumCompletionsAtCursor(Editor.Text, Editor.TextArea.Caret.Offset, _eventType);
                if (enumValues.Count > 0)
                    ShowCompletionWindow(enumValues, 0);
            }
            else if (char.IsLetter(ch) || ch == '_')
            {
                if (_completionWindow != null)
                    return;

                var allItems = ScribanCompletionData.GetTopLevelCompletions(Editor.Text, Editor.TextArea.Caret.Offset, _eventType);
                ShowCompletionWindow(allItems, 1);
            }
        }
        catch (Exception ex)
        {
            // 補完候補の生成・表示で例外が発生してもエディタの操作を妨げないように握り潰す
            LogHost.Default.Warn(ex, "コード補完の候補表示で例外が発生しました");
            CloseCompletionWindowSafely();
        }
    }

    /// <summary>
    /// 補完ウィンドウを安全に閉じる (例外発生時のクリーンアップ用)
    /// </summary>
    private void CloseCompletionWindowSafely()
    {
        try
        {
            _completionWindow?.Close();
        }
        catch (Exception ex)
        {
            LogHost.Default.Warn(ex, "補完ウィンドウのクローズに失敗しました");
        }
        _completionWindow = null;
    }

    private void ShowCompletionWindow(List<ICompletionData> items, int startOffset)
    {
        _completionWindow = new CompletionWindow(Editor.TextArea)
        {
            MinWidth = 200,
        };
        _completionWindow.StartOffset = Editor.TextArea.Caret.Offset - startOffset;

        // テーマからカラーを取得 (Fluent ライクなドック系の色を使用)
        var popupBg = GetThemeBrush("DockTitleBackgroundColor", "#DD505050");
        var popupFg = GetThemeBrush("ForegroundColor", "#F1F1F1");
        var popupBorder = GetThemeBrush("DockBackgroundColor", "#DD808080");

        // CompletionWindow 内の Popup 内 CompletionTipContentControl (説明ツールチップ) を取得
        var tipPopup = ((ILogical)_completionWindow).LogicalChildren
            .OfType<Popup>()
            .FirstOrDefault(p => p.Child is CompletionTipContentControl);
        if (tipPopup?.Child is CompletionTipContentControl tipContent)
        {
            tipContent.Background = popupBg;
            tipContent.Foreground = popupFg;
            tipContent.BorderBrush = popupBorder;
            tipContent.BorderThickness = new Thickness(1);
            tipContent.CornerRadius = new CornerRadius(6);
            tipContent.Padding = new Thickness(10, 8);
            tipContent.MaxWidth = 400;

            // 説明文 (TextBlock) を折り返す
            tipContent.Styles.Add(new Style(s => s.OfType<TextBlock>())
            {
                Setters =
                {
                    new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap),
                },
            });

            // ツールチップ Popup のホストウィンドウ (PopupRoot) を透明にする
            // (CornerRadius で透過した四隅から PopupRoot の白い背景が見えてしまうため)
            tipPopup.Styles.Add(new Style(s => s.OfType<PopupRoot>())
            {
                Setters =
                {
                    new Setter(TemplatedControl.BackgroundProperty, Brushes.Transparent),
                },
            });
        }

        // CompletionList の見た目を Border でラップして角丸・縁を確実に反映させる
        var completionList = _completionWindow.CompletionList;
        completionList.Background = Brushes.Transparent;
        completionList.Foreground = popupFg;
        var listBorder = new Border
        {
            Background = popupBg,
            BorderBrush = popupBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(2),
        };
        // 順序が重要: 先に Popup の Child を Border に差し替えて CompletionList を解放してから付け直す
        _completionWindow.Child = listBorder;
        listBorder.Child = completionList;

        foreach (var item in items)
            _completionWindow.CompletionList.CompletionData.Add(item);

        _completionWindow.Show();

        // Show() 後にテンプレートが適用されるので、ListBox の背景も透明にして外側 Border を見せる
        if (_completionWindow.CompletionList.ListBox is { } listBox)
        {
            listBox.Background = Brushes.Transparent;
            listBox.Foreground = popupFg;
            listBox.BorderThickness = new Thickness(0);
        }

        _completionWindow.Closed += (_, _) => _completionWindow = null;
    }

    private string GetWordBeforeDot(int dotOffset)
    {
        var text = Editor.Text;
        var end = dotOffset;
        var start = end - 1;

        while (start >= 0 && (char.IsLetterOrDigit(text[start]) || text[start] == '_'))
            start--;

        start++;
        if (start >= end)
            return "";

        return text[start..end];
    }

    private async void OkButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            Scriban.Template.Parse(Editor.Text);
        }
        catch (Exception ex)
        {
            var result = await new FAContentDialog
            {
                Title = "テンプレートシンタックスエラー",
                Content = $"テンプレートにシンタックスエラーがあります:\n\n{ex.Message}\n\nこのままにしますか？",
                PrimaryButtonText = "編集する",
                SecondaryButtonText = "戻る"
            }.ShowAsync(this);

            if (result != FAContentDialogResult.Primary)
                return;
        }

        _isResultOk = true;
        Close();
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        _isResultOk = false;
        Close();
    }
}
