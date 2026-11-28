using System;
using System.ComponentModel;

namespace KyoshinEewViewer.JmaXmlParser;

/// <summary>
/// XML の要素に対応する型。<see cref="JmaXmlAttribute"/> を付けた partial メンバーの実装をソースジェネレーターが生成する<br/>
/// 型引数を 1 つ持つ partial クラスに付ける。型引数には読み取りの単位 (<see cref="JmaXmlViewAttribute"/>) が入る
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class JmaXmlTypeAttribute : Attribute;

/// <summary>
/// ルート要素に対応する型
/// </summary>
/// <param name="elementName">ルート要素の名前</param>
[AttributeUsage(AttributeTargets.Class)]
public sealed class JmaXmlRootAttribute(string elementName) : Attribute
{
	public string ElementName => elementName;
}

/// <summary>
/// メンバーと XML の対応
/// </summary>
/// <remarks>
/// 経路は要素名を <c>/</c> で区切り、各要素名の後ろに <c>!</c> (必須) か <c>?</c> (任意) を付ける。必須の要素がない場合は、メンバーを参照した時点で <see cref="JmaXmlParseException"/> になる
/// <list type="bullet">
/// <item><c>Name!</c> 子要素のテキスト、または子要素に対応する型</item>
/// <item><c>Area*</c> 同名の子要素の一覧 (<see cref="JmaXmlList{T}"/>)</item>
/// <item><c>ForecastInt!/From!</c> 子要素の下の要素。何段でも書ける</item>
/// <item><c>Item?/Kind*/Property!</c> 同名の要素それぞれの子要素の一覧</item>
/// <item><c>@type!</c> 自身の属性</item>
/// <item><c>Epicenter!@rank!</c> 子要素の属性</item>
/// <item><c>.</c> 自身のテキスト</item>
/// <item><c>.name</c> 自身の要素名</item>
/// </list>
/// 値の型は string / int / float / DateTimeOffset と、その null 許容型。<c>bool TryGetXxx(out T value)</c> の形の partial メソッドにも付けられる
/// </remarks>
/// <param name="path">経路</param>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method)]
public sealed class JmaXmlAttribute(string path) : Attribute
{
	public string Path => path;

	/// <summary>
	/// <c>xsi:nil="true"</c> の要素を、欠落ではなく値が null の要素として扱う
	/// </summary>
	public bool Nil { get; set; }
}

/// <summary>
/// 読み取りの単位。partial 構造体に付ける<br/>
/// この型を型引数に持つ型に対して参照されているメンバーだけを読み取るコードが生成される。参照されていないメンバーは読み取られず、参照すると例外になる
/// </summary>
[AttributeUsage(AttributeTargets.Struct)]
public sealed class JmaXmlViewAttribute : Attribute;

/// <summary>
/// <see cref="JmaXmlTypeAttribute"/> の型を持つアセンブリにソースジェネレーターが付ける目印
/// </summary>
[AttributeUsage(AttributeTargets.Assembly)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class JmaXmlTypeLibraryAttribute : Attribute;

/// <summary>
/// 読み取りの単位が読み取るメンバーの一覧。ソースジェネレーターが付け、別のアセンブリからの参照の検査に使う
/// </summary>
[AttributeUsage(AttributeTargets.Struct)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class JmaXmlSelectedAttribute(string members) : Attribute
{
	public string Members => members;
}
