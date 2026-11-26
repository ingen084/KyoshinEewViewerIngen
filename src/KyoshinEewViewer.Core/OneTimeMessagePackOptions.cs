using MessagePack;

namespace KyoshinEewViewer.Core;

/// <summary>
/// 起動時などに一度だけ読み込むデータ向けの MessagePack の設定
/// </summary>
public static class OneTimeMessagePackOptions
{
	/// <summary>
	/// 読み込みごとに専用のバッファプールを持つ設定を作る
	/// </summary>
	/// <remarks>既定の共有プールは、展開に使った配列をプロセスの終了まで保持する。専用のプールにすることで、読み込み後に解放されるようにする</remarks>
	public static MessagePackSerializerOptions Create(MessagePackCompression compression = MessagePackCompression.None)
		=> MessagePackSerializerOptions.Standard.WithCompression(compression).WithPool(new SequencePool());
}
