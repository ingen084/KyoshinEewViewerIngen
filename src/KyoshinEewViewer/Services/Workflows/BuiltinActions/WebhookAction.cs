using Avalonia.Controls;
using ReactiveUI;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

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

	public override Control DisplayControl => new WebhookActionControl() { DataContext = this };

	private string _url = "";
	public string Url
	{
		get => _url;
		set => this.RaiseAndSetIfChanged(ref _url, value);
	}

	private string _latestResponse = "";
	[JsonIgnore]
	public string LatestResponse
	{
		get => _latestResponse;
		set => this.RaiseAndSetIfChanged(ref _latestResponse, value);
	}

	[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "<Pending>")]
	public async override Task ExecuteAsync(WorkflowEvent content)
	{
		try
		{
			using var request = new HttpRequestMessage(HttpMethod.Post, Url)
			{
				Content = new StringContent(JsonSerializer.Serialize(content, JsonSerializerOptions), Encoding.UTF8, "application/json")
			};

			var sw = Stopwatch.StartNew();
			using var response = await WebHookHttpClient.SendAsync(request);
			var responseText = await response.Content.ReadAsStringAsync();
			sw.Stop();
			LatestResponse = $"レスポンスタイム: {sw.ElapsedMilliseconds}ms\nステータスコード: {(int)response.StatusCode}\nレスポンス: {responseText}";
		}
		catch (Exception e)
		{
			LatestResponse = $"例外が発生しました。\n{e.Message}";
		}
	}
}
