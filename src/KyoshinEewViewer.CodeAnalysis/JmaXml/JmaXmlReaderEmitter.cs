using System.Text;

namespace KyoshinEewViewer.CodeAnalysis.JmaXml;

/// <summary>
/// 読み取りの単位が参照するメンバーだけを、1 回の走査で読み取るコードを出力する
/// </summary>
internal sealed class JmaXmlReaderEmitter(ViewModel view, TypeSet types, HashSet<string> selected)
{
	private sealed class Plan
	{
		public string Name = "";
		public List<PropModel> Texts = [], Attrs = [], Firsts = [];
		public PropModel? List;
		/// <summary>この要素それぞれの子を一覧に加えるメンバー</summary>
		public PropModel? ItemList;
		public List<Plan> Nested = [];
		public string? Flag;
		public bool IsObject => Firsts.Count > 0 || List != null;
		public bool Singleton => List == null && ItemList == null;
	}

	private readonly List<string> _usedTypes = [];
	private int _id;

	/// <summary>出力できなかった組み合わせ</summary>
	public List<string> Errors { get; } = [];

	private string Uid(string prefix) => prefix + (++_id);

	private string TypeName(string key) => "global::" + key + "<" + view.FullName + ">";

	private string ReaderName(string key)
	{
		var index = _usedTypes.IndexOf(key);
		if (index < 0)
		{
			index = _usedTypes.Count;
			_usedTypes.Add(key);
		}
		return "Read" + index + "_" + key.Substring(key.LastIndexOf('.') + 1);
	}

	private static Plan Get(List<Plan> plans, string name)
	{
		foreach (var plan in plans)
		{
			if (plan.Name == name)
				return plan;
		}
		var created = new Plan { Name = name };
		plans.Add(created);
		return created;
	}

	private static List<Plan> BuildPlans(List<PropModel> props)
	{
		var plans = new List<Plan>();
		foreach (var p in props)
		{
			var scope = plans;
			Plan? owner = null;
			foreach (var container in p.Containers)
			{
				owner = Get(scope, container.Name);
				owner.Flag = container.Flag;
				scope = owner.Nested;
			}

			switch (p.Kind)
			{
				case "text":
					Get(scope, p.Elem!).Texts.Add(p);
					break;
				case "attr" when owner != null:
					owner.Attrs.Add(p);
					break;
				case "obj":
					Get(scope, p.Elem!).Firsts.Add(p);
					break;
				case "list" when p.ItemChild != null:
					Get(scope, p.Elem!).ItemList = p;
					break;
				case "list":
					Get(scope, p.Elem!).List = p;
					break;
			}
		}
		return plans;
	}

	/// <summary>バッファ上の位置 (start, length) にある値をメンバーへ入れる</summary>
	private static string Assign(PropModel p, string target, string start, string length)
	{
		var field = target + "." + p.Field;
		var span = "t.Slice(" + start + ", " + length + ")";
		if (p.Conv == "str" || p.Kind == "selfname")
			return field + " = " + span + ".ToUtf16();";
		var method = p.Conv switch { "int" => "TryToInt32", "float" => "TryToFloat32", _ => "TryToDateTimeOffset" };
		return target + "." + p.HasField + " = " + span + "." + method + "(out " + field + ");";
	}

	private void AttributeLoop(List<string> lines, List<PropModel> attrs, string target, string ind, string? nil)
	{
		if (attrs.Count == 0 && nil == null)
		{
			lines.Add(ind + "t.SkipAttributes();");
			return;
		}
		string an = Uid("an"), av = Uid("av");
		lines.Add(ind + "while (t.ReadAttribute(out var " + an + ", out var " + av + "))");
		lines.Add(ind + "{");
		var keyword = "if";
		if (nil != null)
		{
			lines.Add(ind + "\tif (" + an + ".SequenceEqual(\"xsi:nil\"u8) || " + an + ".SequenceEqual(\"nil\"u8)) " + nil + " = " + av + ".SequenceEqual(\"true\"u8);");
			keyword = "else if";
		}
		foreach (var p in attrs)
		{
			lines.Add(ind + "\t" + keyword + " (" + an + ".SequenceEqual(\"" + p.Attr + "\"u8)) { " + Assign(p, target, "t.AttributeValueStart", av + ".Length") + " }");
			keyword = "else if";
		}
		lines.Add(ind + "}");
	}

	/// <summary>子要素の走査ループの前に置く宣言と、ループの中身</summary>
	private void Children(List<Plan> plans, string target, string ind, string exit, List<string> declarations, List<string> body)
	{
		string? remaining = null;
		if (plans.Count > 0 && plans.All(p => p.Singleton))
		{
			remaining = Uid("rem");
			declarations.Add(ind + "var " + remaining + " = " + plans.Count + ";");
		}
		var name = Uid("n");
		body.Add(ind + "\tvar " + name + " = t.Name;");
		var keyword = "if";
		foreach (var plan in plans)
		{
			body.Add(ind + "\t" + keyword + " (" + name + ".SequenceEqual(\"" + plan.Name + "\"u8))");
			body.Add(ind + "\t{");
			var inner = ind + "\t\t";
			if (plan.Singleton)
			{
				// 同じ名前の要素が複数あっても最初の 1 つだけを読む
				var seen = Uid("seen");
				declarations.Add(ind + "var " + seen + " = false;");
				body.Add(inner + "if (" + seen + ") " + JmaXmlNames.Support + ".Skip(ref t);");
				body.Add(inner + "else");
				body.Add(inner + "{");
				body.Add(inner + "\t" + seen + " = true;");
				Element(body, plan, target, inner + "\t");
				if (remaining != null)
					body.Add(inner + "\tif (--" + remaining + " == 0) { " + exit + " }");
				body.Add(inner + "}");
			}
			else
				Element(body, plan, target, inner);
			body.Add(ind + "\t}");
			keyword = "else if";
		}
		body.Add(ind + "\t" + (plans.Count > 0 ? "else " : "") + JmaXmlNames.Support + ".Skip(ref t);");
	}

	private void Unsupported(List<string> lines, string ind, string element)
	{
		Errors.Add(element);
		lines.Add(ind + JmaXmlNames.Support + ".Skip(ref t);");
	}

	/// <summary>開始タグの名前まで読んだ子要素 1 つ分</summary>
	private void Element(List<string> lines, Plan plan, string target, string ind)
	{
		if (plan.ItemList != null)
		{
			if (plan.IsObject || plan.Texts.Count > 0 || plan.Attrs.Count > 0 || plan.Nested.Count > 0)
			{
				Unsupported(lines, ind, plan.Name);
				return;
			}
			var p = plan.ItemList;
			string its = Uid("ns"), itl = Uid("nl"), found = Uid("found");
			lines.Add(ind + "int " + its + " = t.NameStart, " + itl + " = t.NameLength;");
			lines.Add(ind + "var " + found + " = false;");
			lines.Add(ind + "t.SkipAttributes();");
			lines.Add(ind + "if (!t.IsEmptyElement)");
			lines.Add(ind + "{");
			lines.Add(ind + "\twhile (" + JmaXmlNames.Support + ".NextChild(ref t, " + its + ", " + itl + "))");
			lines.Add(ind + "\t{");
			lines.Add(ind + "\t\tif (!t.Name.SequenceEqual(\"" + p.ItemChild + "\"u8)) { " + JmaXmlNames.Support + ".Skip(ref t); continue; }");
			lines.Add(ind + "\t\t" + found + " = true;");
			lines.Add(ind + "\t\t(" + target + "." + p.Field + " ??= new " + JmaXmlNames.List + "<" + TypeName(p.TypeKey!) + ">()).Add(" + ReaderName(p.TypeKey!) + "(ref t));");
			lines.Add(ind + "\t\tt.SkipElementContent();");
			lines.Add(ind + "\t\tbreak;");
			lines.Add(ind + "\t}");
			lines.Add(ind + "}");
			if (p.Required)
				lines.Add(ind + "if (!" + found + ") " + target + "." + p.MissingItemField + " = true;");
			return;
		}

		if (plan.IsObject)
		{
			var keys = plan.Firsts.Select(f => f.TypeKey!).Concat(plan.List != null ? [plan.List.TypeKey!] : Array.Empty<string>()).Distinct().ToList();
			if (plan.Texts.Count > 0 || plan.Attrs.Count > 0 || plan.Nested.Count > 0 || (plan.List != null && keys.Count > 1))
			{
				Unsupported(lines, ind, plan.Name);
				return;
			}
			if (keys.Count == 1)
			{
				var value = Uid("c");
				lines.Add(ind + "var " + value + " = " + ReaderName(keys[0]) + "(ref t);");
				foreach (var first in plan.Firsts)
					lines.Add(ind + target + "." + first.Field + " ??= " + value + ";");
				if (plan.List != null)
					lines.Add(ind + "(" + target + "." + plan.List.Field + " ??= new " + JmaXmlNames.List + "<" + TypeName(keys[0]) + ">()).Add(" + value + ");");
				return;
			}
			// 同じ要素を別の型として読むメンバーが両方使われている場合は、要素の先頭へ戻って読み直す
			var start = Uid("ns");
			lines.Add(ind + "var " + start + " = t.NameStart;");
			for (var i = 0; i < plan.Firsts.Count; i++)
			{
				if (i > 0)
				{
					lines.Add(ind + "t.Position = " + start + " - 1;");
					lines.Add(ind + "t.Read();");
				}
				lines.Add(ind + target + "." + plan.Firsts[i].Field + " = " + ReaderName(plan.Firsts[i].TypeKey!) + "(ref t);");
			}
			return;
		}

		if (plan.Nested.Count > 0 && plan.Texts.Count > 0)
		{
			Unsupported(lines, ind, plan.Name);
			return;
		}

		string ns = Uid("ns"), nl = Uid("nl");
		lines.Add(ind + "int " + ns + " = t.NameStart, " + nl + " = t.NameLength;");
		if (plan.Flag != null)
			lines.Add(ind + target + "." + plan.Flag + " = true;");
		string? nil = null;
		if (plan.Texts.Any(p => p.Conv == "ndt"))
		{
			nil = Uid("nil");
			lines.Add(ind + "var " + nil + " = false;");
		}
		AttributeLoop(lines, plan.Attrs, target, ind, nil);

		if (plan.Nested.Count > 0)
		{
			var declarations = new List<string>();
			var body = new List<string>();
			Children(plan.Nested, target, ind + "\t", "t.SkipElementContent(); break;", declarations, body);
			lines.Add(ind + "if (!t.IsEmptyElement)");
			lines.Add(ind + "{");
			lines.AddRange(declarations);
			lines.Add(ind + "\twhile (" + JmaXmlNames.Support + ".NextChild(ref t, " + ns + ", " + nl + "))");
			lines.Add(ind + "\t{");
			lines.AddRange(body);
			lines.Add(ind + "\t}");
			lines.Add(ind + "}");
		}
		else if (plan.Texts.Count > 0)
		{
			string ts = Uid("ts"), tl = Uid("tl");
			lines.Add(ind + "if (" + JmaXmlNames.Support + ".TryLeafText(ref t, " + ns + ", " + nl + ", out var " + ts + ", out var " + tl + "))");
			lines.Add(ind + "{");
			foreach (var p in plan.Texts)
			{
				if (p.Conv == "ndt")
				{
					var field = target + "." + p.Field;
					var has = target + "." + p.HasField;
					var value = Uid("v");
					lines.Add(ind + "\tif (" + nil + ") { " + field + " = null; " + has + " = true; }");
					lines.Add(ind + "\telse if (t.Slice(" + ts + ", " + tl + ").TryToDateTimeOffset(out var " + value + ")) { " + field + " = " + value + "; " + has + " = true; }");
				}
				else
					lines.Add(ind + "\t" + Assign(p, target, ts, tl));
			}
			lines.Add(ind + "}");
		}
		else
			lines.Add(ind + JmaXmlNames.Support + ".SkipContent(ref t);");
	}

	private string Reader(string key, bool isRoot)
	{
		var props = types.Types.TryGetValue(key, out var type)
			? type.Props.Where(p => p.Error == null && selected.Contains(key + "|" + p.Name)).ToList()
			: [];
		var selfAttrs = props.Where(p => p.Kind == "attr" && p.Containers.Count == 0).ToList();
		var selfTexts = props.Where(p => p.Kind == "selftext").ToList();
		var plans = BuildPlans(props);
		ulong mask = 0;
		foreach (var p in props)
			mask |= 1UL << p.Bit;

		var lines = new List<string>
		{
			"\tprivate static " + TypeName(key) + " " + ReaderName(key) + "(ref " + JmaXmlNames.Tokenizer + " t)",
			"\t{",
			"\t\tint es = t.NameStart, el = t.NameLength;",
			"\t\tvar o = new " + TypeName(key) + "();",
			"\t\to.__mask = 0x" + mask.ToString("X") + "UL;",
		};
		foreach (var p in props.Where(p => p.Kind == "selfname"))
			lines.Add("\t\t" + Assign(p, "o", "es", "el"));
		AttributeLoop(lines, selfAttrs, "o", "\t\t", null);
		if (selfTexts.Count > 0)
		{
			if (plans.Count > 0)
				Errors.Add(key);
			lines.Add("\t\t" + JmaXmlNames.Support + ".TryLeafText(ref t, es, el, out var ts, out var tl);");
			foreach (var p in selfTexts)
				lines.Add("\t\t" + Assign(p, "o", "ts", "tl"));
		}
		else if (plans.Count == 0)
			lines.Add("\t\t" + JmaXmlNames.Support + ".SkipContent(ref t);");
		else
		{
			var declarations = new List<string>();
			var body = new List<string>();
			// 必要な子がすべて単一で、すべて読み終えたら残りを読み飛ばす。ルートでは解析を打ち切る
			Children(plans, "o", "\t\t", isRoot ? "return o;" : "t.SkipElementContent(); return o;", declarations, body);
			lines.Add("\t\tif (t.IsEmptyElement) return o;");
			lines.AddRange(declarations);
			lines.Add("\t\twhile (" + JmaXmlNames.Support + ".NextChild(ref t, es, el))");
			lines.Add("\t\t{");
			lines.AddRange(body);
			lines.Add("\t\t}");
		}
		lines.Add("\t\treturn o;");
		lines.Add("\t}");
		return string.Join("\n", lines);
	}

	public string Emit()
	{
		var sb = new StringBuilder();
		sb.AppendLine("// <auto-generated/>");
		sb.AppendLine("#nullable enable");
		sb.AppendLine("using System;");
		sb.AppendLine("using " + JmaXmlNames.Namespace + ".Streaming;");
		sb.AppendLine();
		var ordered = selected.OrderBy(s => s, StringComparer.Ordinal).ToList();
		sb.AppendLine("// 読み取るメンバー: " + ordered.Count + " 個");
		foreach (var item in ordered)
			sb.AppendLine("//   " + item.Replace('|', '.'));
		sb.AppendLine();

		var root = types.Types.Values.FirstOrDefault(t => t.RootElement != null);
		if (root == null)
		{
			Errors.Add("[JmaXmlRoot]");
			return sb.ToString();
		}

		if (view.Namespace.Length > 0)
			sb.AppendLine("namespace " + view.Namespace + ";").AppendLine();
		sb.AppendLine("[global::" + JmaXmlNames.SelectedAttribute.Substring(0, JmaXmlNames.SelectedAttribute.Length - "Attribute".Length) + "(\"" + string.Join(";", ordered) + "\")]");
		sb.AppendLine("partial struct " + view.Name + " : " + JmaXmlNames.View + "<" + view.Name + ">");
		sb.AppendLine("{");
		sb.AppendLine("\tpublic static " + TypeName(root.Key) + " Read(global::System.ReadOnlySpan<byte> utf8Xml)");
		sb.AppendLine("\t{");
		sb.AppendLine("\t\tvar t = new " + JmaXmlNames.Tokenizer + "(utf8Xml);");
		sb.AppendLine("\t\tif (!t.Read() || t.Kind != " + JmaXmlNames.TokenKind + ".StartElement)");
		sb.AppendLine("\t\t\tthrow new global::System.FormatException(\"ルートノードがありません\");");
		sb.AppendLine("\t\tif (!t.Name.SequenceEqual(\"" + root.RootElement + "\"u8))");
		sb.AppendLine("\t\t\tthrow new " + JmaXmlNames.ParseException + "(\"ルートノードが " + root.RootElement + " ではありません\");");
		sb.AppendLine("\t\treturn " + ReaderName(root.Key) + "(ref t);");
		sb.AppendLine("\t}");

		for (var i = 0; i < _usedTypes.Count; i++)
		{
			sb.AppendLine();
			sb.AppendLine(Reader(_usedTypes[i], i == 0));
		}
		sb.AppendLine("}");
		return sb.ToString();
	}
}
