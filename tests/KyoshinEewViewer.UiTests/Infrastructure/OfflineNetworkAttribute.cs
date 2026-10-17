using System.Reflection;
using Xunit.v3;

namespace KyoshinEewViewer.UiTests.Infrastructure;

// 全テストへ適用し、テスト本体が例外を握り潰しても終了時に違反を検出する。
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class OfflineNetworkAttribute : BeforeAfterTestAttribute
{
	public override void Before(MethodInfo methodUnderTest, IXunitTest test)
		=> NetworkAudit.Begin(methodUnderTest.DeclaringType!.FullName + "." + methodUnderTest.Name);

	public override void After(MethodInfo methodUnderTest, IXunitTest test) => NetworkAudit.End();
}

public sealed class NetworkAssemblyFixture : IDisposable
{
	public NetworkAssemblyFixture()
	{
		NetworkIsolation.Require();
		NetworkAudit.Initialize();
	}

	public void Dispose() => NetworkAudit.Finish();
}
