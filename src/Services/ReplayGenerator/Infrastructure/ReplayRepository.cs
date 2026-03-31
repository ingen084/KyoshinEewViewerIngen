using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace ReplayGenerator.Infrastructure;

public class ReplayRepository
{
	private readonly string _connectionString;
	private readonly ILogger<ReplayRepository> _logger;

	public ReplayRepository(string connectionString, ILogger<ReplayRepository> logger)
	{
		_connectionString = connectionString;
		_logger = logger;
	}

	public async Task<string> InsertReplayFile(DateTime startTime, DateTime endTime, string objectKey, int? fileSizeBytes)
	{
		var id = Guid.NewGuid().ToString();
		await using var conn = new NpgsqlConnection(_connectionString);
		await conn.OpenAsync();

		await using var cmd = new NpgsqlCommand(
			"INSERT INTO replay_files (id, start_time, end_time, object_key, file_size_bytes, created_at) VALUES (@id, @start, @end, @key, @size, NOW())",
			conn);
		cmd.Parameters.AddWithValue("id", Guid.Parse(id));
		cmd.Parameters.AddWithValue("start", startTime);
		cmd.Parameters.AddWithValue("end", endTime);
		cmd.Parameters.AddWithValue("key", objectKey);
		cmd.Parameters.AddWithValue("size", (object?)fileSizeBytes ?? DBNull.Value);

		await cmd.ExecuteNonQueryAsync();
		_logger.LogInformation($"リプレイファイルをDBに登録しました: {id}");
		return id;
	}

	public async Task InsertTrigger(string replayFileId, string triggerType, string eventId)
	{
		await using var conn = new NpgsqlConnection(_connectionString);
		await conn.OpenAsync();

		await using var cmd = new NpgsqlCommand(
			"INSERT INTO replay_file_triggers (id, replay_file_id, trigger_type, event_id, created_at) VALUES (gen_random_uuid(), @fileId, @type, @eventId, NOW()) ON CONFLICT DO NOTHING",
			conn);
		cmd.Parameters.AddWithValue("fileId", Guid.Parse(replayFileId));
		cmd.Parameters.AddWithValue("type", triggerType);
		cmd.Parameters.AddWithValue("eventId", eventId);

		await cmd.ExecuteNonQueryAsync();
	}
}
