using KyoshinEewViewer.Series.KyoshinMonitor.Models;
using KyoshinEewViewer.Series.KyoshinMonitor.Workflow;
using KyoshinEewViewer.Services.Workflows.BuiltinActions;

namespace KyoshinEewViewer.Tests.Services;

public class ExecuteFileActionTests
{
	[Fact(DisplayName = "既定のアプリで開くでバッチファイルを実行すると、引用符で囲んだパスと引数で起動され、終了後に cmd が残らない")]
	public async Task 既定のアプリで開くでバッチファイルを実行してもcmdが残らない()
	{
		if (!OperatingSystem.IsWindows())
			return;

		var directory = Path.Combine(Path.GetTempPath(), $"kevi test {Guid.NewGuid():N}");
		Directory.CreateDirectory(directory);
		try
		{
			var output = Path.Combine(directory, "output.txt");
			await File.WriteAllTextAsync(Path.Combine(directory, "run test.bat"), "@echo off\r\necho [%*]> \"%~dp0output.txt\"\r\n");

			var action = new ExecuteFileAction
			{
				FilePath = $"\"{Path.Combine(directory, "run test.bat")}\" foo bar",
				WorkingDirectory = directory,
				UseShellExecute = true,
				WaitToEnd = true,
			};
			await action.ExecuteAsync(EewEvent.FromEewModel(null!, EewEventType.New, EewMock.NORMAL, false));

			// start は起動したプロセスの終了を待たないため、バッチファイルが書き出すまで待つ
			string? written = null;
			Assert.True(await WaitUntilAsync(() => TryReadAllText(output, out written)), "バッチファイルが実行されませんでした");
			Assert.Equal("[foo bar]", written?.Trim());

			// cmd が残っていると作業ディレクトリが使用中になり、削除できない
			Assert.True(await WaitUntilAsync(() => TryDeleteDirectory(directory)), "バッチファイルの終了後も cmd が残っています");
		}
		finally
		{
			TryDeleteDirectory(directory);
		}
	}

	private static async Task<bool> WaitUntilAsync(Func<bool> condition)
	{
		var deadline = DateTime.UtcNow.AddSeconds(10);
		while (!condition())
		{
			if (DateTime.UtcNow > deadline)
				return false;
			await Task.Delay(100);
		}
		return true;
	}

	private static bool TryReadAllText(string path, out string? text)
	{
		text = null;
		try
		{
			if (!File.Exists(path))
				return false;
			text = File.ReadAllText(path);
			return true;
		}
		catch (IOException)
		{
			return false;
		}
	}

	private static bool TryDeleteDirectory(string path)
	{
		try
		{
			if (Directory.Exists(path))
				Directory.Delete(path, true);
			return true;
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			return false;
		}
	}
}
