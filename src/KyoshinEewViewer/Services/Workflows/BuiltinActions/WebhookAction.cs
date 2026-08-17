using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Series.KyoshinMonitor.Workflow;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using KyoshinEewViewer.Core;

namespace KyoshinEewViewer.Services.Workflows.BuiltinActions;

public class WebhookAction : WorkflowAction
{
	private static HttpClient WebHookHttpClient { get; } = new();
	private static JsonSerializerOptions JsonSerializerOptions { get; } = new()
	{
		Converters =
		{
			new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
		},
	};

	[JsonIgnore]
	public override Control DisplayControl => new WebhookActionControl() { DataContext = this };

	private string _url = "";
	public string Url
	{
		get => _url;
		set => SetProperty(ref _url, value);
	}

	private string _latestResponse = "";
	[JsonIgnore]
	public string LatestResponse
	{
		get => _latestResponse;
		set => SetProperty(ref _latestResponse, value);
	}

	private bool _injectPointForecast;
	public bool InjectPointForecast
	{
		get => _injectPointForecast;
		set => SetProperty(ref _injectPointForecast, value);
	}

	[JsonIgnore]
	public bool CanInjectPointForecast
		=> ServiceLocator.Current.GetService<KyoshinEewViewerConfiguration>()?.Eew.EnableExternalPointForecast ?? false;

	/// <summary>
	/// 地点予測を取り込む場合のタイムアウト
	/// EEW は1秒程度の間隔で更新されるため、遅れた値を待たない
	/// </summary>
	private static readonly TimeSpan PointForecastTimeout = TimeSpan.FromSeconds(2);

	/// <summary>
	/// 地点予測として読み込むレスポンスの上限
	/// </summary>
	private const int MaxPointForecastResponseSize = 16 * 1024;

	[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "<Pending>")]
	public async override Task ExecuteAsync(WorkflowEvent content)
	{
		// 地点予測として取り込む場合はレスポンスを待つ意味が薄れるため、短いタイムアウトを設定する
		var isInjectTarget = InjectPointForecast && CanInjectPointForecast && content is EewEvent { IsTest: false };
		using var timeoutSource = isInjectTarget ? new CancellationTokenSource(PointForecastTimeout) : null;

		try
		{
			using var request = new HttpRequestMessage(HttpMethod.Post, Url)
			{
				Content = new StringContent(JsonSerializer.Serialize(content, JsonSerializerOptions), Encoding.UTF8, "application/json")
			};

			var sw = Stopwatch.StartNew();
			using var response = await WebHookHttpClient.SendAsync(request, timeoutSource?.Token ?? CancellationToken.None);
			var responseText = await response.Content.ReadAsStringAsync(timeoutSource?.Token ?? CancellationToken.None);
			sw.Stop();

			var injectResult = isInjectTarget && content is EewEvent eewEvent
				? InjectPointForecasts(eewEvent, response.IsSuccessStatusCode, responseText)
				: null;

			if (responseText.Length > 100)
				responseText = responseText[..100] + "...";
			LatestResponse = $"レスポンスタイム: {sw.ElapsedMilliseconds}ms\nステータスコード: {(int)response.StatusCode}\nレスポンス: {responseText}" +
				(injectResult == null ? "" : $"\n{injectResult}");
		}
		catch (OperationCanceledException) when (timeoutSource?.IsCancellationRequested ?? false)
		{
			LatestResponse = $"地点予測の取り込みのタイムアウト({PointForecastTimeout.TotalSeconds:0.#}秒)に達したため中断しました。";
		}
		catch (Exception e)
		{
			LatestResponse = $"例外が発生しました。\n{e.Message}";
		}
	}

	/// <summary>
	/// レスポンスを地点予測として取り込む
	/// </summary>
	/// <returns>利用者に表示する取り込み結果</returns>
	private string InjectPointForecasts(EewEvent content, bool isSuccessStatusCode, string responseText)
	{
		if (!isSuccessStatusCode)
			return "地点予測の取り込み: ステータスコードが成功以外のためスキップしました。";
		if (responseText.Length > MaxPointForecastResponseSize)
			return $"地点予測の取り込み: レスポンスが大きすぎます({MaxPointForecastResponseSize / 1024}KiB以内)。";
		// リプレイ中はホストごとに別のコントローラーになるため、イベントの発火元から注入先を解決する
		if (content.SourceEewController?.PointForecastController is not { } pointForecastController)
			return "地点予測の取り込み: 発火元のEEWコントローラーを特定できませんでした。";

		// 提供元が指定されていない場合はワークフローを識別できる値を使用する
		var workflow = FindWorkflow();
		return pointForecastController.InjectFromWebhookResponse(
			content.EewId,
			content.SerialNo,
			responseText,
			workflow?.Id.ToString() ?? Url,
			string.IsNullOrWhiteSpace(workflow?.Name) ? "Webhook" : workflow.Name);
	}
}
