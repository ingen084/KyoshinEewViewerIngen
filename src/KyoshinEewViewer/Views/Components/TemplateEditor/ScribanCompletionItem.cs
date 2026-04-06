using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;

namespace KyoshinEewViewer.Views.Components.TemplateEditor;

internal class ScribanCompletionItem(string text, string description, ScribanSymbolKind kind = ScribanSymbolKind.Property) : ICompletionData
{
    public IBrush? Foreground => null;
    public IImage? Image => null;
    public string Text { get; } = text;
    public object Content => CreateContent();
    public object Description => description;
    public double Priority => 0;

    public ScribanSymbolKind Kind => kind;

    private static IBrush B(string hex) => new SolidColorBrush(Color.Parse(hex));

    // ScribanSymbolKind ごとのバッジ表示スタイル
    private static readonly Dictionary<ScribanSymbolKind, (string Label, IBrush Background, IBrush Foreground)> BadgeStyles = new()
    {
        [ScribanSymbolKind.Keyword]       = ("K", B("#569CD6"), Brushes.White),
        [ScribanSymbolKind.Constant]      = ("C", B("#4FC1FF"), Brushes.White),
        [ScribanSymbolKind.BuiltinObject] = ("O", B("#4EC9B0"), Brushes.White),
        [ScribanSymbolKind.Method]        = ("M", B("#DCDCAA"), Brushes.Black),
        [ScribanSymbolKind.Property]      = ("P", B("#9CDCFE"), Brushes.Black),
        [ScribanSymbolKind.LocalVariable] = ("V", B("#B5CEA8"), Brushes.Black),
        [ScribanSymbolKind.LoopPseudo]    = ("L", B("#C586C0"), Brushes.White),
    };

    private static readonly (string Label, IBrush Background, IBrush Foreground) UnknownBadge =
        ("?", B("#888888"), Brushes.White);

    private Control CreateContent()
    {
        var (label, badgeBg, badgeFg) = BadgeStyles.TryGetValue(kind, out var v) ? v : UnknownBadge;

        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
        };
        panel.Children.Add(new Border
        {
            Background = badgeBg,
            CornerRadius = new CornerRadius(3),
            Width = 16,
            Height = 16,
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = label,
                Foreground = badgeFg,
                FontSize = 10,
                FontWeight = FontWeight.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        });
        panel.Children.Add(new TextBlock
        {
            Text = Text,
            VerticalAlignment = VerticalAlignment.Center,
        });
        return panel;
    }

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        textArea.Document.Replace(completionSegment, Text);
    }
}
