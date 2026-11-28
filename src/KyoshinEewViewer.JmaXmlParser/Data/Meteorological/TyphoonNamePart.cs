namespace KyoshinEewViewer.JmaXmlParser.Data.Meteorological;

[JmaXmlType]
public sealed partial class TyphoonNamePart<TView>
{
	/// <summary>
	/// 台風の英字の呼名
	/// </summary>
	[JmaXml("Name?")]
	public partial string? Name { get; }

	/// <summary>
	/// 台風のカナの呼名
	/// </summary>
	[JmaXml("NameKana?")]
	public partial string? NameKana { get; }

	/// <summary>
	/// 台風番号
	/// </summary>
	[JmaXml("Number?")]
	public partial string? Number { get; }

	/// <summary>
	/// 台風番号
	/// <list type="bullet">
	/// <item>台風発生</item>
	/// <item>台風発生（域外から入る）</item>
	/// <item>台風消滅（域外へ出る）</item>
	/// <item>台風消滅（温帯低気圧化）</item>
	/// <item>台風消滅（熱帯低気圧化）</item>
	/// <item>台風発生の可能性が小さくなった</item>
	/// <item>発表間隔変更（毎時から３時間毎）</item>
	/// <item>発表間隔変更（３時間毎から毎時）</item>
	/// <item>台風発生予想</item>
	/// <item>温帯低気圧化しつつある</item>
	/// </list>
	/// </summary>
	[JmaXml("Remark?")]
	public partial string? Remark { get; }
}
