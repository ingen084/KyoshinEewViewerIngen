using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ReplayGenerator.Domain;
using ReplayGenerator.Infrastructure;
using ReplayGenerator.Services;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);

var redisUrl = Environment.GetEnvironmentVariable("REDIS_URL") ?? "localhost:6379";
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL") ?? "";
var s3Endpoint = Environment.GetEnvironmentVariable("S3_ENDPOINT") ?? "";
var s3Bucket = Environment.GetEnvironmentVariable("S3_BUCKET") ?? "eqmonitor-replay";
var s3AccessKey = Environment.GetEnvironmentVariable("S3_ACCESS_KEY") ?? "";
var s3SecretKey = Environment.GetEnvironmentVariable("S3_SECRET_KEY") ?? "";
var internalApiUrl = Environment.GetEnvironmentVariable("EQMONITOR_INTERNAL_API_URL");

builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisUrl));
builder.Services.AddSingleton<ValkeyStateManager>();
builder.Services.AddSingleton<ShakeDetectionTracker>();
builder.Services.AddSingleton<EarthquakeTracker>();
builder.Services.AddSingleton(_ =>
{
	var logger = _.GetRequiredService<ILogger<ReplayRepository>>();
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

var host = builder.Build();
await host.RunAsync();
