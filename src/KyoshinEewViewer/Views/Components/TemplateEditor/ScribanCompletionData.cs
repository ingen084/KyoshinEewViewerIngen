using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AvaloniaEdit.CodeCompletion;

namespace KyoshinEewViewer.Views.Components.TemplateEditor;

internal static partial class ScribanCompletionData
{
    /// <summary>
    /// 同一 kind の補完アイテムをまとめて生成するヘルパー
    /// </summary>
    private static List<ICompletionData> Items(ScribanSymbolKind kind, params (string Name, string Desc)[] entries)
        => [.. entries.Select(e => (ICompletionData)new ScribanCompletionItem(e.Name, e.Desc, kind))];

    private static readonly List<ICompletionData> ScribanKeywords =
    [
        .. Items(ScribanSymbolKind.Keyword,
            ("if", "条件分岐"),
            ("else", "条件分岐の else 節"),
            ("end", "ブロック終了"),
            ("for", "ループ (for <var> in <collection>)"),
            ("in", "for ループの in キーワード"),
            ("while", "while ループ"),
            ("break", "ループ中断"),
            ("continue", "ループ継続"),
            ("func", "関数定義"),
            ("ret", "関数からの戻り値"),
            ("capture", "出力キャプチャ"),
            ("wrap", "ラッパー関数"),
            ("import", "オブジェクトのインポート"),
            ("readonly", "読み取り専用変数"),
            ("with", "スコープ設定"),
            ("case", "case/when 分岐"),
            ("when", "case の条件"),
            ("tablerow", "テーブル行ループ"),
            ("and", "論理AND"),
            ("or", "論理OR"),
            ("not", "論理NOT")),

        .. Items(ScribanSymbolKind.Constant,
            ("true", "真"),
            ("false", "偽"),
            ("null", "null 値"),
            ("nil", "null 値"),
            ("empty", "空値")),

        .. Items(ScribanSymbolKind.BuiltinObject,
            ("string", "文字列操作関数 (string.*)"),
            ("math", "数学関数 (math.*)"),
            ("date", "日付関数 (date.*)"),
            ("timespan", "時間間隔関数 (timespan.*)"),
            ("array", "配列操作関数 (array.*)"),
            ("object", "オブジェクト操作関数 (object.*)"),
            ("regex", "正規表現関数 (regex.*)"),
            ("html", "HTMLエスケープ関数 (html.*)")),
    ];

    private static readonly Dictionary<string, List<ICompletionData>> BuiltinMembers = new()
    {
        ["string"] = Items(ScribanSymbolKind.Method,
            ("append", "文字列を末尾に追加"),
            ("capitalize", "先頭を大文字にする"),
            ("contains", "文字列を含むか"),
            ("downcase", "小文字に変換"),
            ("upcase", "大文字に変換"),
            ("empty", "空文字列か判定"),
            ("ends_with", "指定文字列で終わるか"),
            ("handleize", "ハンドル化"),
            ("lstrip", "左側の空白を除去"),
            ("rstrip", "右側の空白を除去"),
            ("strip", "両側の空白を除去"),
            ("pad_left", "左側をパディング"),
            ("pad_right", "右側をパディング"),
            ("pluralize", "複数形変換"),
            ("prepend", "先頭に文字列を追加"),
            ("remove", "文字列を削除"),
            ("remove_first", "最初の一致を削除"),
            ("remove_last", "最後の一致を削除"),
            ("replace", "文字列を置換"),
            ("replace_first", "最初の一致を置換"),
            ("size", "文字列の長さ"),
            ("slice", "部分文字列を取得"),
            ("split", "文字列を分割"),
            ("starts_with", "指定文字列で始まるか"),
            ("strip_newlines", "改行を除去"),
            ("truncate", "指定文字数に切り詰め"),
            ("truncatewords", "指定単語数に切り詰め")),

        ["math"] = Items(ScribanSymbolKind.Method,
            ("abs", "絶対値"),
            ("ceil", "切り上げ"),
            ("floor", "切り捨て"),
            ("round", "四捨五入"),
            ("divided_by", "除算"),
            ("minus", "減算"),
            ("plus", "加算"),
            ("times", "乗算"),
            ("modulo", "剰余"),
            ("is_number", "数値かどうか判定"),
            ("format", "数値フォーマット")),

        ["date"] = Items(ScribanSymbolKind.Method,
            ("now", "現在日時"),
            ("add_days", "日数を加算"),
            ("add_hours", "時間を加算"),
            ("add_minutes", "分を加算"),
            ("add_months", "月数を加算"),
            ("add_seconds", "秒を加算"),
            ("add_years", "年数を加算"),
            ("to_string", "文字列に変換 (date.to_string <date> <format>)"),
            ("parse", "文字列から日時をパース")),

        ["timespan"] = Items(ScribanSymbolKind.Method,
            ("from_days", "日数から TimeSpan を生成"),
            ("from_hours", "時間から TimeSpan を生成"),
            ("from_minutes", "分から TimeSpan を生成"),
            ("from_seconds", "秒から TimeSpan を生成"),
            ("parse", "文字列からパース")),

        ["array"] = Items(ScribanSymbolKind.Method,
            ("add", "要素を追加"),
            ("add_range", "複数要素を追加"),
            ("compact", "null を除去"),
            ("concat", "配列を結合"),
            ("contains", "要素を含むか"),
            ("first", "最初の要素"),
            ("last", "最後の要素"),
            ("join", "要素を文字列で結合"),
            ("map", "プロパティの値を抽出"),
            ("reverse", "逆順にする"),
            ("size", "要素数"),
            ("sort", "ソート"),
            ("uniq", "重複を除去"),
            ("remove_at", "インデックス位置の要素を削除"),
            ("insert_at", "インデックス位置に挿入")),

        ["object"] = Items(ScribanSymbolKind.Method,
            ("keys", "オブジェクトのキー一覧"),
            ("size", "プロパティ数"),
            ("typeof", "型名を取得"),
            ("has_key", "キーが存在するか"),
            ("has_value", "値が存在するか")),

        ["regex"] = Items(ScribanSymbolKind.Method,
            ("match", "正規表現でマッチ"),
            ("replace", "正規表現で置換"),
            ("split", "正規表現で分割"),
            ("escape", "正規表現文字をエスケープ")),

        ["html"] = Items(ScribanSymbolKind.Method,
            ("escape", "HTMLエスケープ"),
            ("strip", "HTMLタグを除去"),
            ("url_encode", "URLエンコード"),
            ("url_escape", "URLエスケープ")),
    };

    /// <summary>
    /// ループ擬似変数 (for.*) の補完候補
    /// </summary>
    private static readonly List<ICompletionData> ForLoopMembers = Items(ScribanSymbolKind.LoopPseudo,
        ("first", "bool - 最初のイテレーションかどうか"),
        ("last", "bool - 最後のイテレーションかどうか"),
        ("index", "int - 現在のインデックス (0始まり)"),
        ("rindex", "int - 末尾からのインデックス (0始まり)"),
        ("length", "int - 全要素数"),
        ("even", "bool - インデックスが偶数かどうか"),
        ("odd", "bool - インデックスが奇数かどうか"),
        ("changed", "bool - 値が前回から変化したかどうか"));

    private static readonly List<ICompletionData> WhileLoopMembers = Items(ScribanSymbolKind.LoopPseudo,
        ("first", "bool - 最初のイテレーションかどうか"),
        ("index", "int - 現在のインデックス (0始まり)"));

    private static readonly List<ICompletionData> TableRowMembers = Items(ScribanSymbolKind.LoopPseudo,
        ("first", "bool - 最初のイテレーションかどうか"),
        ("last", "bool - 最後のイテレーションかどうか"),
        ("index", "int - 現在のインデックス (0始まり)"),
        ("rindex", "int - 末尾からのインデックス (0始まり)"),
        ("col", "int - 現在の列番号"),
        ("col0", "int - 0始まりの列番号"),
        ("col_first", "bool - 行の最初の列かどうか"),
        ("col_last", "bool - 行の最後の列かどうか"),
        ("row", "int - 現在の行番号"));

    /// <summary>
    /// プリミティブ型とみなす型の一覧（プロパティ展開しない型）
    /// </summary>
    private static bool IsTerminalType(Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;
        return t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal)
            || t == typeof(DateTime) || t == typeof(DateTimeOffset) || t == typeof(Guid) || t == typeof(TimeSpan);
    }

    /// <summary>
    /// 型のpublicプロパティから補完候補を生成する
    /// </summary>
    private static List<ICompletionData> GetPropertiesFromType(Type type)
    {
        // NullabilityInfoContext はスレッドセーフではないが、UI スレッドからの呼び出しのみのため許容
        var nullabilityContext = new NullabilityInfoContext();
        return type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<JsonIgnoreAttribute>() == null)
            .Select(p =>
            {
                var propType = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
                var typeName = GetFriendlyTypeName(propType);
                if (IsNullableProperty(p, nullabilityContext))
                    typeName += "?";

                var hasChildren = !IsTerminalType(propType) && !IsCollectionType(propType);
                var typeDesc = hasChildren ? $"{typeName} ({p.Name}.*)" : typeName;

                // [Description] 属性があれば説明を追加
                var descAttr = p.GetCustomAttribute<DescriptionAttribute>();
                var description = descAttr != null
                    ? $"{descAttr.Description}\n({typeDesc})"
                    : typeDesc;

                return (ICompletionData)new ScribanCompletionItem(p.Name, description, ScribanSymbolKind.Property);
            })
            .ToList();
    }

    /// <summary>
    /// プロパティが null 許容かどうかを判定する。
    /// 値型は <see cref="Nullable{T}"/> を、参照型は NRT 注釈 (NullableAttribute) を確認する。
    /// </summary>
    private static bool IsNullableProperty(PropertyInfo property, NullabilityInfoContext context)
    {
        // Nullable<T> 値型
        if (Nullable.GetUnderlyingType(property.PropertyType) != null)
            return true;

        // 値型 (非 Nullable<T>) は決して null にならない
        if (property.PropertyType.IsValueType)
            return false;

        // 参照型は NRT 注釈を確認 (一部のメンバで例外を投げることがあるため try-catch)
        try
        {
            return context.Create(property).ReadState == NullabilityState.Nullable;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// コレクション型かどうかを判定する
    /// </summary>
    private static bool IsCollectionType(Type type)
        => type != typeof(string) && type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

    /// <summary>
    /// 型名を短い表示名にする
    /// </summary>
    private static string GetFriendlyTypeName(Type type)
    {
        if (type.IsArray)
            return $"{GetFriendlyTypeName(type.GetElementType()!)}[]";

        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying != null)
            return $"{GetFriendlyTypeName(underlying)}?";

        if (type == typeof(string)) return "string";
        if (type == typeof(int)) return "int";
        if (type == typeof(float)) return "float";
        if (type == typeof(double)) return "double";
        if (type == typeof(bool)) return "bool";
        if (type == typeof(DateTime)) return "DateTime";
        if (type == typeof(DateTimeOffset)) return "DateTimeOffset";
        if (type == typeof(Guid)) return "Guid";
        if (type == typeof(TimeSpan)) return "TimeSpan";

        return type.Name;
    }

    /// <summary>
    /// 指定された型からプロパティ名に対応するプロパティの型を取得する
    /// </summary>
    private static Type? ResolvePropertyType(Type type, string propertyName)
    {
        var prop = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (prop == null)
            return null;
        return Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
    }

    /// <summary>
    /// "a.b.c" のドットパスを、現在のスタック上のローカル変数 → イベント型のルートから探索して解決する
    /// </summary>
    private static Type? ResolvePathFromContext(string path, IEnumerable<(string Keyword, string? VarName, Type? VarType)> stack, Type? eventType)
    {
        var parts = path.Split('.');
        if (parts.Length == 0)
            return null;

        var rootName = parts[0];
        Type? currentType = null;

        // ローカル変数を優先
        foreach (var (_, name, type) in stack)
        {
            if (name == rootName && type != null)
            {
                currentType = type;
                break;
            }
        }

        // ローカル変数になければイベント型のプロパティを探す
        if (currentType == null && eventType != null)
            currentType = ResolvePropertyType(eventType, rootName);

        if (currentType == null)
            return null;

        // 残りのパスを辿る
        for (var i = 1; i < parts.Length; i++)
        {
            currentType = ResolvePropertyType(currentType, parts[i]);
            if (currentType == null)
                return null;
        }

        return currentType;
    }

    /// <summary>
    /// コレクション型から要素型を取得する (T[] / IEnumerable&lt;T&gt;)
    /// </summary>
    private static Type? GetElementType(Type type)
    {
        if (type.IsArray)
            return type.GetElementType();

        var enumerable = type.GetInterfaces()
            .Concat([type])
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        return enumerable?.GetGenericArguments()[0];
    }

    [GeneratedRegex(@"\bfor\s+([a-zA-Z_]\w*)\s+in\s+([a-zA-Z_][\w\.]*)")]
    private static partial Regex ForLoopRegex();

    [GeneratedRegex(@"\bcase\s+([a-zA-Z_][\w\.]*)")]
    private static partial Regex CaseRegex();

    [GeneratedRegex(@"\b(if|for|while|func|capture|wrap|case|tablerow|unless)\b")]
    private static partial Regex BlockStartRegex();

    [GeneratedRegex(@"\bend\b")]
    private static partial Regex BlockEndRegex();

    [GeneratedRegex(@"([a-zA-Z_][\w\.]*)\s*(==|!=)\s*$")]
    private static partial Regex CompareRegex();

    [GeneratedRegex(@"\bwhen\s*$")]
    private static partial Regex WhenRegex();

    /// <summary>
    /// 指定オフセットがコードブロック ({{ }} または {% %}) の内側かどうかを判定する。
    /// 構文ハイライトと補完判定の双方で利用される共有実装。
    /// </summary>
    public static bool IsInsideCodeBlock(string text, int offset)
    {
        if (string.IsNullOrEmpty(text) || offset <= 0)
            return false;

        var searchEnd = Math.Min(offset, text.Length) - 1;
        if (searchEnd < 1)
            return false;

        // {{ }} ブロック
        var lastDoubleOpen = text.LastIndexOf("{{", searchEnd, StringComparison.Ordinal);
        var lastDoubleClose = text.LastIndexOf("}}", searchEnd, StringComparison.Ordinal);
        if (lastDoubleOpen >= 0 && lastDoubleClose < lastDoubleOpen)
            return true;

        // {% %} ブロック
        var lastStmtOpen = text.LastIndexOf("{%", searchEnd, StringComparison.Ordinal);
        var lastStmtClose = text.LastIndexOf("%}", searchEnd, StringComparison.Ordinal);
        return lastStmtOpen >= 0 && lastStmtClose < lastStmtOpen;
    }

    /// <summary>
    /// 指定位置における現在のブロックスタック (内側→外側) を取得する
    /// </summary>
    private static List<(string Keyword, string? VarName, Type? VarType, string? CaseExpr)> GetBlockStack(string text, int offset, Type? eventType)
    {
        if (string.IsNullOrEmpty(text))
            return [];

        var beforeCursor = text[..Math.Min(offset, text.Length)];

        // ブロック開始/終了のトークンを位置順に収集 (種類のみ、型解決は後回し)
        var tokens = new List<(int Pos, bool IsStart, string Keyword)>();

        foreach (Match m in BlockStartRegex().Matches(beforeCursor))
            tokens.Add((m.Index, true, m.Value));

        foreach (Match m in BlockEndRegex().Matches(beforeCursor))
            tokens.Add((m.Index, false, "end"));

        tokens.Sort((a, b) => a.Pos.CompareTo(b.Pos));

        // 順次処理してローカル変数を解決
        // (内側のループ変数は外側のループ変数を参照できる必要がある)
        var stack = new Stack<(string Keyword, string? VarName, Type? VarType, string? CaseExpr)>();
        foreach (var token in tokens)
        {
            if (token.IsStart)
            {
                string? varName = null;
                Type? varType = null;
                string? caseExpr = null;

                if (token.Keyword == "for")
                {
                    var forMatch = ForLoopRegex().Match(beforeCursor, token.Pos);
                    if (forMatch.Success && forMatch.Index == token.Pos)
                    {
                        varName = forMatch.Groups[1].Value;
                        var path = forMatch.Groups[2].Value;
                        // 現在のスタックにあるローカル変数も解決対象に含める
                        var collectionType = ResolvePathFromContext(path, stack.Select(s => (s.Keyword, s.VarName, s.VarType)), eventType);
                        if (collectionType != null)
                            varType = GetElementType(collectionType);
                    }
                }
                else if (token.Keyword == "case")
                {
                    var caseMatch = CaseRegex().Match(beforeCursor, token.Pos);
                    if (caseMatch.Success && caseMatch.Index == token.Pos)
                        caseExpr = caseMatch.Groups[1].Value;
                }

                stack.Push((token.Keyword, varName, varType, caseExpr));
            }
            else if (stack.Count > 0)
            {
                stack.Pop();
            }
        }

        return [.. stack];
    }

    /// <summary>
    /// 指定位置でスコープ内にあるローカル変数とその型を取得する
    /// </summary>
    private static Dictionary<string, Type> GetLocalVariablesInScope(string text, int offset, Type? eventType)
    {
        var result = new Dictionary<string, Type>();
        foreach (var entry in GetBlockStack(text, offset, eventType))
        {
            if (entry.VarName != null && entry.VarType != null && !result.ContainsKey(entry.VarName))
                result[entry.VarName] = entry.VarType;
        }
        return result;
    }

    public static List<ICompletionData> GetTopLevelCompletions(string text, int offset, Type? eventType)
    {
        var items = new List<ICompletionData>(ScribanKeywords);

        if (eventType != null)
            items.AddRange(GetPropertiesFromType(eventType));

        // ブロックスタックを取得
        var blockStack = GetBlockStack(text, offset, eventType);

        // ローカル変数を追加
        var added = new HashSet<string>();
        foreach (var entry in blockStack)
        {
            if (entry.VarName == null || entry.VarType == null || !added.Add(entry.VarName))
                continue;
            var typeName = GetFriendlyTypeName(entry.VarType);
            var hasChildren = !IsTerminalType(entry.VarType) && !IsCollectionType(entry.VarType);
            var description = hasChildren ? $"{typeName} (ローカル変数, {entry.VarName}.*)" : $"{typeName} (ローカル変数)";
            items.Add(new ScribanCompletionItem(entry.VarName, description, ScribanSymbolKind.LocalVariable));
        }

        // ループ擬似変数 (for.*, while.*, tablerow.*) のヒントを追加
        if (blockStack.Any(b => b.Keyword == "for"))
            items.Add(new ScribanCompletionItem("for", "ループ情報 (for.first, for.last, for.index など)", ScribanSymbolKind.LoopPseudo));
        if (blockStack.Any(b => b.Keyword == "while"))
            items.Add(new ScribanCompletionItem("while", "ループ情報 (while.first, while.index)", ScribanSymbolKind.LoopPseudo));
        if (blockStack.Any(b => b.Keyword == "tablerow"))
            items.Add(new ScribanCompletionItem("tablerow", "ループ情報 (tablerow.first, tablerow.col など)", ScribanSymbolKind.LoopPseudo));

        return items;
    }

    public static List<ICompletionData> GetMembersFor(string objectName, string text, int offset, Type? eventType)
    {
        // Scriban ビルトインオブジェクトのメンバー
        if (BuiltinMembers.TryGetValue(objectName, out var builtinMembers))
            return builtinMembers;

        // ループ擬似変数 (for.*, while.*, tablerow.*) - ループ内でのみ有効
        if (objectName is "for" or "while" or "tablerow")
        {
            var blockStack = GetBlockStack(text, offset, eventType);
            if (blockStack.Any(b => b.Keyword == objectName))
            {
                return objectName switch
                {
                    "for" => ForLoopMembers,
                    "while" => WhileLoopMembers,
                    "tablerow" => TableRowMembers,
                    _ => [],
                };
            }
        }

        // ローカル変数のメンバーを解決
        var locals = GetLocalVariablesInScope(text, offset, eventType);
        if (locals.TryGetValue(objectName, out var localType))
        {
            if (!IsTerminalType(localType) && !IsCollectionType(localType))
                return GetPropertiesFromType(localType);
            return [];
        }

        // イベント型のプロパティからオブジェクト型のメンバーを解決
        if (eventType != null)
        {
            var propType = ResolvePropertyType(eventType, objectName);
            if (propType != null && !IsTerminalType(propType) && !IsCollectionType(propType))
                return GetPropertiesFromType(propType);
        }

        return [];
    }

    /// <summary>
    /// カーソル位置が enum 値の文字列リテラルを期待するコンテキスト
    /// (例: <c>EventSubType == "</c> や <c>case 内の when "</c>) かどうかを判定し、
    /// 該当する enum の値リストを補完候補として返す
    /// </summary>
    public static List<ICompletionData> GetEnumCompletionsAtCursor(string text, int offset, Type? eventType)
    {
        if (string.IsNullOrEmpty(text))
            return [];

        var beforeCursor = text[..Math.Min(offset, text.Length)];
        // 末尾の " を除外して評価する
        if (!beforeCursor.EndsWith('"'))
            return [];
        var beforeQuote = beforeCursor[..^1];

        var blockStack = GetBlockStack(text, offset, eventType);
        var stackForResolve = blockStack.Select(s => (s.Keyword, s.VarName, s.VarType));

        // パターン1: <expr> == " or <expr> != "
        var compareMatch = CompareRegex().Match(beforeQuote);
        if (compareMatch.Success && compareMatch.Index + compareMatch.Length == beforeQuote.Length)
        {
            var expr = compareMatch.Groups[1].Value;
            var type = ResolvePathFromContext(expr, stackForResolve, eventType);
            if (type != null && type.IsEnum)
                return EnumValuesAsCompletions(type);
        }

        // パターン2: when " (case ブロック内)
        var whenMatch = WhenRegex().Match(beforeQuote);
        if (whenMatch.Success && whenMatch.Index + whenMatch.Length == beforeQuote.Length)
        {
            // 内側 (スタックの先頭) から最初に見つかる case ブロックを使う
            var caseEntry = blockStack.FirstOrDefault(b => b.Keyword == "case" && b.CaseExpr != null);
            if (caseEntry.CaseExpr != null)
            {
                var type = ResolvePathFromContext(caseEntry.CaseExpr, stackForResolve, eventType);
                if (type != null && type.IsEnum)
                    return EnumValuesAsCompletions(type);
            }
        }

        return [];
    }

    /// <summary>
    /// enum の値名を補完候補として返す
    /// </summary>
    private static List<ICompletionData> EnumValuesAsCompletions(Type enumType)
    {
        return Enum.GetNames(enumType)
            .Select(name => (ICompletionData)new ScribanCompletionItem(name, $"{enumType.Name} の値", ScribanSymbolKind.Constant))
            .ToList();
    }
}
