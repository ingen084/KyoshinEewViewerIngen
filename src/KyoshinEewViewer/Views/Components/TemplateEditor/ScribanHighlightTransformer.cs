using System.Collections.Generic;
using System.Text.RegularExpressions;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace KyoshinEewViewer.Views.Components.TemplateEditor;

/// <summary>
/// Scriban テンプレートの構文ハイライトを行う DocumentColorizingTransformer
/// </summary>
internal partial class ScribanHighlightTransformer(bool isDark) : DocumentColorizingTransformer
{
    private sealed record HighlightPalette(
        IBrush Delimiter,
        IBrush Keyword,
        IBrush String,
        IBrush Number,
        IBrush Comment,
        IBrush Pipe,
        IBrush Builtin,
        IBrush Bool);

    private static IBrush B(string hex) => new SolidColorBrush(Color.Parse(hex));

    // VS Code Dark+ ベース
    private static readonly HighlightPalette DarkPalette = new(
        Delimiter: B("#569CD6"),
        Keyword:   B("#569CD6"),
        String:    B("#CE9178"),
        Number:    B("#B5CEA8"),
        Comment:   B("#6A9955"),
        Pipe:      B("#C586C0"),
        Builtin:   B("#4EC9B0"),
        Bool:      B("#569CD6"));

    // VS Code Light+ ベース
    private static readonly HighlightPalette LightPalette = new(
        Delimiter: B("#0000FF"),
        Keyword:   B("#0000FF"),
        String:    B("#A31515"),
        Number:    B("#098658"),
        Comment:   B("#008000"),
        Pipe:      B("#AF00DB"),
        Builtin:   B("#267F99"),
        Bool:      B("#0000FF"));

    private readonly HighlightPalette _palette = isDark ? DarkPalette : LightPalette;

    private static readonly HashSet<string> Keywords =
    [
        "if", "else", "end", "for", "in", "while", "break", "continue",
        "func", "ret", "capture", "wrap", "import", "readonly", "with",
        "case", "when", "tablerow", "unless", "do", "and", "or", "not"
    ];

    private static readonly HashSet<string> BooleanConstants = ["true", "false", "null", "nil", "empty"];

    private static readonly HashSet<string> BuiltinObjects =
    [
        "string", "math", "date", "timespan", "array", "object", "regex", "html", "include"
    ];

    [GeneratedRegex(@"\{\{|\}\}|\{%|%\}|\{#|#\}")]
    private static partial Regex DelimiterRegex();

    [GeneratedRegex(@"""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\])*'|`[^`]*`|\$""(?:\\.|[^""\\])*""")]
    private static partial Regex StringRegex();

    [GeneratedRegex(@"#[^\r\n]*")]
    private static partial Regex CommentRegex();

    [GeneratedRegex(@"\b\d+(?:\.\d+)?\b")]
    private static partial Regex NumberRegex();

    [GeneratedRegex(@"\b[a-zA-Z_]\w*\b")]
    private static partial Regex WordRegex();

    [GeneratedRegex(@"\|")]
    private static partial Regex PipeRegex();

    protected override void ColorizeLine(DocumentLine line)
    {
        var lineText = CurrentContext.Document.GetText(line);
        var lineStart = line.Offset;
        var lineEnd = lineStart + lineText.Length;
        var fullText = CurrentContext.Document.Text;

        // 行ごとにブロック境界を検出し、ブロック内部のみ色付けする
        var codeRanges = GetCodeRanges(lineText, lineStart, fullText);

        // デリミタの色付け (行全体に適用)
        foreach (Match m in DelimiterRegex().Matches(lineText))
            ChangeLinePart(lineStart + m.Index, lineStart + m.Index + m.Length,
                e => e.TextRunProperties.SetForegroundBrush(_palette.Delimiter));

        // コードブロック範囲内のトークンを色付け
        foreach (var (rangeStart, rangeEnd) in codeRanges)
        {
            var blockOffset = rangeStart - lineStart;
            var blockLength = rangeEnd - rangeStart;
            if (blockOffset < 0 || blockLength <= 0)
                continue;
            var blockText = lineText.Substring(
                System.Math.Max(0, blockOffset),
                System.Math.Min(blockLength, lineText.Length - System.Math.Max(0, blockOffset)));

            ColorizeMatches(CommentRegex(), blockText, rangeStart, lineStart, lineEnd, _palette.Comment);
            ColorizeMatches(StringRegex(),  blockText, rangeStart, lineStart, lineEnd, _palette.String);
            ColorizeMatches(NumberRegex(),  blockText, rangeStart, lineStart, lineEnd, _palette.Number);
            ColorizeMatches(PipeRegex(),    blockText, rangeStart, lineStart, lineEnd, _palette.Pipe);

            // ワード（キーワード・ビルトイン・ブール値）
            foreach (Match m in WordRegex().Matches(blockText))
            {
                var word = m.Value;
                IBrush? brush = null;
                if (Keywords.Contains(word))
                    brush = _palette.Keyword;
                else if (BooleanConstants.Contains(word))
                    brush = _palette.Bool;
                else if (BuiltinObjects.Contains(word))
                    brush = _palette.Builtin;

                if (brush != null)
                {
                    var absStart = rangeStart + m.Index;
                    var absEnd = absStart + m.Length;
                    if (absStart >= lineStart && absEnd <= lineEnd)
                        ChangeLinePart(absStart, absEnd, e => e.TextRunProperties.SetForegroundBrush(brush));
                }
            }
        }
    }

    /// <summary>
    /// blockText に対して regex をマッチさせ、行範囲内に収まるものだけを brush で着色する
    /// </summary>
    private void ColorizeMatches(Regex regex, string blockText, int rangeStart, int lineStart, int lineEnd, IBrush brush)
    {
        foreach (Match m in regex.Matches(blockText))
        {
            var absStart = rangeStart + m.Index;
            var absEnd = absStart + m.Length;
            if (absStart >= lineStart && absEnd <= lineEnd)
                ChangeLinePart(absStart, absEnd, e => e.TextRunProperties.SetForegroundBrush(brush));
        }
    }

    /// <summary>
    /// 行内のコードブロック範囲を検出する（{{ }} / {% %} の内側）
    /// </summary>
    private static List<(int Start, int End)> GetCodeRanges(string lineText, int lineStart, string fullText)
    {
        var ranges = new List<(int, int)>();

        // 行の先頭がコードブロック内かどうかを判定
        var inBlock = ScribanCompletionData.IsInsideCodeBlock(fullText, lineStart);
        var pos = 0;
        var blockStart = inBlock ? lineStart : -1;

        while (pos < lineText.Length - 1)
        {
            var c0 = lineText[pos];
            var c1 = lineText[pos + 1];

            if (!inBlock && ((c0 == '{' && c1 == '{') || (c0 == '{' && c1 == '%')))
            {
                blockStart = lineStart + pos + 2;
                inBlock = true;
                pos += 2;
            }
            else if (inBlock && ((c0 == '}' && c1 == '}') || (c0 == '%' && c1 == '}')))
            {
                if (blockStart >= 0)
                    ranges.Add((blockStart, lineStart + pos));
                inBlock = false;
                pos += 2;
            }
            else
            {
                pos++;
            }
        }

        // 行末まで開いたままの場合
        if (inBlock && blockStart >= 0)
            ranges.Add((blockStart, lineStart + lineText.Length));

        return ranges;
    }
}
