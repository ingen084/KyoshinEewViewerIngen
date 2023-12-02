using System;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Core.Models;

#nullable disable

[JsonSerializable(typeof(GitHubRelease[]))]
internal partial class GitHubReleaseJsonContext : JsonSerializerContext
{
}


public class GitHubRelease
{
	[JsonPropertyName("name")]
	public string Name { get; set; }
	[JsonPropertyName("tag_name")]
	public string TagName { get; set; }
	[JsonPropertyName("body")]
	public string Body { get; set; }
	[JsonPropertyName("url")]
	public string Url { get; set; }
	[JsonPropertyName("draft")]
	public bool Draft { get; set; }
	[JsonPropertyName("prerelease")]
	public bool Prerelease { get; set; }
	[JsonPropertyName("created_at")]
	public DateTime CreatedAt { get; set; }
	[JsonPropertyName("published_at")]
	public DateTime PublishedAt { get; set; }
	[JsonPropertyName("assets")]
	public GitHubReleaseAsset[] Assets { get; set; }

	public static async Task<GitHubRelease[]> GetReleasesAsync(HttpClient client, string url)
	{
		using var response = await client.GetStreamAsync(url);
		return await JsonSerializer.DeserializeAsync<GitHubRelease[]>(response, GitHubReleaseJsonContext.Default.GitHubReleaseArray);
	}
}

public class GitHubReleaseAsset
{
	[JsonPropertyName("name")]
	public string Name { get; set; }
	[JsonPropertyName("label")]
	public string Label { get; set; }
	[JsonPropertyName("content_type")]
	public string ContentType { get; set; }
	[JsonPropertyName("state")]
	public string State { get; set; }
	[JsonPropertyName("size")]
	public int Size { get; set; }
	[JsonPropertyName("download_count")]
	public int DownloadCount { get; set; }
	[JsonPropertyName("created_at")]
	public DateTime CreatedAt { get; set; }
	[JsonPropertyName("updated_at")]
	public DateTime UpdatedAt { get; set; }
	[JsonPropertyName("browser_download_url")]
	public string BrowserDownloadUrl { get; set; }
}
