using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Prometheus;
using ReplayGenerator.Domain;
using ReplayGenerator.Infrastructure;
using ReplayGenerator.Services;
using StackExchange.Redis;

namespace ReplayGenerator;

internal class Program
{
	public static async Task Main(string[] args)
	{
		CultureInfo.CurrentCulture = new CultureInfo("ja-JP");

		var port = int.Parse(Environment.GetEnvironmentVariable("PORT") ?? "5000");
		Environment.SetEnvironmentVariable(
			"ASPNETCORE_URLS",
			$"http://0.0.0.0:{port}");

		var builder = WebApplication.CreateBuilder(args);

		var redisUrl = Environment.GetEnvironmentVariable("REDIS_URL") ?? "localhost:6379";
		var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL") ?? "";
		var s3Endpoint = Environment.GetEnvironmentVariable("S3_ENDPOINT") ?? "";
		var s3Bucket = Environment.GetEnvironmentVariable("S3_BUCKET") ?? "eqmonitor-replay";
		var s3AccessKey = Environment.GetEnvironmentVariable("S3_ACCESS_KEY") ?? "";
		var s3SecretKey = Environment.GetEnvironmentVariable("S3_SECRET_KEY") ?? "";
		var internalApiUrl = Environment.GetEnvironmentVariable("EQMONITOR_INTERNAL_API_URL");

		builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisUrl));
		builder.Services.AddSingleton<TimeProvider>(_ => TimeProvider.System);
		builder.Services.AddSingleton<ValkeyStateManager>();
		builder.Services.AddSingleton<ShakeDetectionTracker>();
		builder.Services.AddSingleton<EarthquakeTracker>();
		builder.Services.AddSingleton(sp =>
		{
			var logger = sp.GetRequiredService<ILogger<ReplayRepository>>();
			return new ReplayRepository(databaseUrl, logger);
		});
		builder.Services.AddSingleton(sp =>
		{
			var logger = sp.GetRequiredService<ILogger<ObjectStorageClient>>();
			return new ObjectStorageClient(s3Endpoint, s3AccessKey, s3SecretKey, s3Bucket, logger);
		});
		builder.Services.AddHttpClient();
		builder.Services.AddSingleton(sp =>
		{
			var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
			var logger = sp.GetRequiredService<ILogger<ReplayFileBuilder>>();
			return new ReplayFileBuilder(httpClientFactory.CreateClient(), logger, internalApiUrl);
		});

		builder.Services.AddHostedService<ReplayGeneratorWorker>();

		var app = builder.Build();

		app.UseRouting();
		app.UseHttpMetrics();

		app.MapMetrics();
		app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
		app.MapGet("/health/ready", async (IConnectionMultiplexer redis) =>
		{
			try
			{
				await redis.GetDatabase().PingAsync();
				return Results.Ok(new { status = "ready" });
			}
			catch
			{
				return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
			}
		});

		await app.RunAsync();
	}
}
