using KyoshinEewViewer.Dmdata;
using KyoshinEewViewer.Dmdata.Exceptions;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace DmdataClientExample
{
	class Program
	{
		static async Task Main()
		{
			Console.WriteLine("DmdataのAPIキーを入力してください>");
			var apiKey = Console.ReadLine();

			using var client = new DmdataApiClient(apiKey, "KyoshinEewViewer.Dmdata;Example");

			try
			{
				// 課金情報を取得
				var billingInfo = await client.GetBillingInfoAsync();
				Console.WriteLine(@$"** 課金情報({billingInfo.Date:yyyy年MM月}) **
今月の課金状況（本日まで）: {billingInfo.Amount.Total}
*課金明細
{string.Join('\n', billingInfo.Items.Select(i => $"  {i.Name}({i.Type}) {i.Subtotal}円"))}
DiMisチャージ残高: {billingInfo.Unpaid}");
			}
			catch (DmdataForbiddenException)
			{
				Console.WriteLine("APIキーが正しくないか、課金情報の取得ができませんでした。 billing.get 権限が必要です。");
			}
			Console.WriteLine();
			try
			{
				// 電文リストを10件取得してみる
				var telegramList = await client.GetTelegramListAsync(limit: 10);
				Console.WriteLine($"** 電文リスト **\n");
				foreach (var item in telegramList.Items)
				{
					Console.WriteLine($@"** {item.Data.Type}
  Key: {item.Key}");
				}
			}
			catch (DmdataForbiddenException)
			{
				Console.WriteLine("APIキーが正しくないか、電文リストが取得できませんでした。 telegram.list 権限が必要です。");
			}
			Console.WriteLine("WebSocketへの接続を行います。 Enterキーで接続");
			Console.ReadLine();

			using var socket = new DmdataSocket(client);
			socket.Connected += (s, e) => Console.WriteLine("EVENT: connected");
			socket.Disconnected += (s, e) => Console.WriteLine("EVENT: disconnected");
			socket.Error += (s, e) => Console.WriteLine("EVENT: error  c:" + e.Code + " e:" + e.Error);
			socket.DataReceived += (s, e) =>
			{
				Console.WriteLine($@"EVENT: data  type: {e.Data.Type} key: {e.Key}
      body: {e.GetBodyString().Substring(0, 10)}...");
			};
			await socket.ConnectAsync(new[] { TelegramCategory.Earthquake }, client.UserAgent);

			Console.ReadLine();
			await socket.DisconnectAsync();
		}
	}
}
