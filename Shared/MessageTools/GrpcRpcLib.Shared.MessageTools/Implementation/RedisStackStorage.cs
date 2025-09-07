using GrpcRpcLib.Shared.Entiteis.Models;
using GrpcRpcLib.Shared.MessageTools.Abstraction;
using StackExchange.Redis;

namespace GrpcRpcLib.Shared.MessageTools.Implementation;


public class RedisStackStorage : IMessageStore
{
	private readonly IConnectionMultiplexer _redis;
	private readonly string _prefix;
	private readonly TimeSpan _defaultTtl = TimeSpan.FromHours(1); // Cleanup TTL

	public RedisStackStorage(string connectionString, string prefix = "")
	{
		_redis = ConnectionMultiplexer.Connect(connectionString);
		_prefix = string.IsNullOrEmpty(prefix) ? "msg" : $"{prefix}_msg";

		// Note: For persistence, configure redis.conf:
		// appendonly yes
		// aof-use-rdb-preamble yes
		// save 60 1000 (RDB snapshot)
		// User must enable in Redis server for durability.
	}

	private string GetKey(Guid id) => $"{_prefix}_{id}";
	private string GetStatusIndexKey(string status) => $"{_prefix}_status_{status}";
	private string GetCreatedAtIndexKey() => $"{_prefix}_created_at";
	private string GetRetryCountIndexKey() => $"{_prefix}_retry_count";

	public async Task SaveAsync(MessageEnvelope message, CancellationToken ct = default)
	{
		var db = _redis.GetDatabase();
		var key = GetKey(message.Id);
		var hashEntries = new[]
		{
			new HashEntry("Id", message.Id.ToString()),
			new HashEntry("Type", message.Type),
			new HashEntry("CorrelationId", message.CorrelationId),
			new HashEntry("ReplyTo", message.ReplyTo),
			new HashEntry("Payload", message.Payload),
			new HashEntry("CreatedAt", message.CreatedAt.ToString("o")),
			new HashEntry("Status", message.Status),
			new HashEntry("RetryCount", message.RetryCount),
			new HashEntry("LastRetryAt", message.LastRetryAt?.ToString("o") ?? ""),
			new HashEntry("ErrorMessage", message.ErrorMessage ?? "")
		};

		await db.HashSetAsync(key, hashEntries);
		await db.SetAddAsync(GetStatusIndexKey(message.Status), message.Id.ToString());
		await db.SortedSetAddAsync(GetCreatedAtIndexKey(), message.Id.ToString(), message.CreatedAt.ToUnixTimeSeconds());
		await db.SortedSetAddAsync(GetRetryCountIndexKey(), message.Id.ToString(), message.RetryCount);

		// Set TTL for Completed/Failed/TimedOut
		if (message.Status == "Completed" || message.Status == "Failed" || message.Status == "TimedOut")
			await db.KeyExpireAsync(key, _defaultTtl);
	}

	public async Task UpdateStatusAsync(Guid id, string status, CancellationToken ct = default)
	{
		var db = _redis.GetDatabase();
		var key = GetKey(id);
		var oldStatus = await db.HashGetAsync(key, "Status");
		await db.HashSetAsync(key, new[] { new HashEntry("Status", status) });
		if (!oldStatus.IsNull)
			await db.SetRemoveAsync(GetStatusIndexKey(oldStatus), id.ToString());
		await db.SetAddAsync(GetStatusIndexKey(status), id.ToString());
		if (status == "Completed" || status == "Failed" || status == "TimedOut")
			await db.KeyExpireAsync(key, _defaultTtl);
	}

	public async Task UpdateStatusAsync(Guid id, string status, string? errorMessage, CancellationToken ct = default)
	{
		var db = _redis.GetDatabase();
		var key = GetKey(id);
		var oldStatus = await db.HashGetAsync(key, "Status");
		await db.HashSetAsync(key, new[]
		{
			new HashEntry("Status", status),
			new HashEntry("ErrorMessage", errorMessage ?? "")
		});
		if (!oldStatus.IsNull)
			await db.SetRemoveAsync(GetStatusIndexKey(oldStatus), id.ToString());
		await db.SetAddAsync(GetStatusIndexKey(status), id.ToString());
		if (status == "Completed" || status == "Failed" || status == "TimedOut")
			await db.KeyExpireAsync(key, _defaultTtl);
	}

	public async Task<MessageEnvelope?> GetAsync(Guid id, CancellationToken ct = default)
	{
		var db = _redis.GetDatabase();
		var key = GetKey(id);
		var entries = await db.HashGetAllAsync(key);
		if (entries.Length == 0) return null;
		return MapToEnvelope(entries);
	}

	public async Task<IEnumerable<MessageEnvelope>> GetPendingAsync(CancellationToken ct = default)
	{
		var db = _redis.GetDatabase();
		var ids = await db.SetMembersAsync(GetStatusIndexKey("ResponsePending"));
		var messages = new List<MessageEnvelope>();
		foreach (var id in ids)
		{
			var msg = await GetAsync(Guid.Parse(id), ct);
			if (msg != null) messages.Add(msg);
		}
		return messages;
	}

	public async Task<IEnumerable<MessageEnvelope>> GetFailedAsync(CancellationToken ct = default)
	{
		var db = _redis.GetDatabase();
		var ids = await db.SetMembersAsync(GetStatusIndexKey("Failed"));
		var messages = new List<MessageEnvelope>();
		foreach (var id in ids)
		{
			var msg = await GetAsync(Guid.Parse(id), ct);
			if (msg != null) messages.Add(msg);
		}
		return messages;
	}

	public async Task<IEnumerable<MessageEnvelope>> GetRetryableAsync(CancellationToken ct = default)
	{
		var db = _redis.GetDatabase();
		var cutoff = DateTime.UtcNow.AddMinutes(-5).ToUnixTimeSeconds();
		var ids = await db.SetMembersAsync(GetStatusIndexKey("Failed"));
		var retryable = new List<MessageEnvelope>();
		foreach (var id in ids)
		{
			var msg = await GetAsync(Guid.Parse(id), ct);
			if (msg != null && msg.RetryCount < 3 && (msg.LastRetryAt == null || msg.LastRetryAt < DateTime.UtcNow.AddMinutes(-5)))
				retryable.Add(msg);
		}
		return retryable;
	}

	public async Task IncrementRetryCountAsync(Guid id, CancellationToken ct = default)
	{
		var db = _redis.GetDatabase();
		var key = GetKey(id);
		var retryCount = await db.HashGetAsync(key, "RetryCount");
		if (!retryCount.IsNull)
		{
			var newCount = (int)retryCount + 1;
			await db.HashSetAsync(key, new[] { new HashEntry("RetryCount", newCount), new HashEntry("LastRetryAt", DateTime.UtcNow.ToString("o")) });
			await db.SortedSetAddAsync(GetRetryCountIndexKey(), id.ToString(), newCount);
		}
	}

	public async Task CleanupOldMessagesAsync(TimeSpan maxAge, CancellationToken ct = default)
	{
		var db = _redis.GetDatabase();
		var cutoff = DateTime.UtcNow.Subtract(maxAge).ToUnixTimeSeconds();
		var ids = await db.SortedSetRangeByScoreAsync(GetCreatedAtIndexKey(), 0, cutoff);
		foreach (var id in ids)
		{
			var key = GetKey(Guid.Parse(id));
			var status = await db.HashGetAsync(key, "Status");
			if (status == "Completed" || status == "Failed" || status == "TimedOut")
			{
				await db.KeyDeleteAsync(key);
				await db.SetRemoveAsync(GetStatusIndexKey(status), id);
				await db.SortedSetRemoveAsync(GetCreatedAtIndexKey(), id);
				await db.SortedSetRemoveAsync(GetRetryCountIndexKey(), id);
			}
		}
	}

	public async Task<Dictionary<string, int>> GetMessageCountsByStatusAsync(CancellationToken ct = default)
	{
		var db = _redis.GetDatabase();
		var statuses = new[] { "TryingToPublish", "Publishing", "ResponsePending", "Failed", "Completed", "Retrying", "Received", "TimedOut" };
		var counts = new Dictionary<string, int>();
		foreach (var status in statuses)
		{
			var count = await db.SetLengthAsync(GetStatusIndexKey(status));
			counts[status] = (int)count;
		}
		return counts;
	}

	public async Task<IEnumerable<MessageEnvelope>> GetOldPendingMessagesAsync(TimeSpan maxAge, CancellationToken ct = default)
	{
		var db = _redis.GetDatabase();
		var cutoff = DateTime.UtcNow.Subtract(maxAge).ToUnixTimeSeconds();
		var ids = await db.SortedSetRangeByScoreAsync(GetCreatedAtIndexKey(), 0, cutoff);
		var messages = new List<MessageEnvelope>();
		foreach (var id in ids)
		{
			var msg = await GetAsync(Guid.Parse(id), ct);
			if (msg != null && msg.Status == "ResponsePending")
				messages.Add(msg);
		}
		return messages;
	}

	public async Task<IEnumerable<MessageEnvelope>> LoadPendingMessagesAsync(CancellationToken ct = default)
	{
		var db = _redis.GetDatabase();
		var ids = await db.SetMembersAsync(GetStatusIndexKey("ResponsePending"));
		var messages = new List<MessageEnvelope>();
		foreach (var id in ids)
		{
			var msg = await GetAsync(Guid.Parse(id), ct);
			if (msg != null) messages.Add(msg);
		}
		return messages;
	}

	private MessageEnvelope MapToEnvelope(HashEntry[] entries)
	{
		var dict = entries.ToDictionary(e => e.Name.ToString(), e => e.Value);
		return new MessageEnvelope
		{
			Id = Guid.Parse(dict["Id"].ToString()),
			Type = dict["Type"].ToString(),
			CorrelationId = dict["CorrelationId"].ToString(),
			ReplyTo = dict["ReplyTo"].ToString(),
			Payload = (byte[])dict["Payload"],
			CreatedAt = DateTime.Parse(dict["CreatedAt"].ToString()),
			Status = dict["Status"].ToString(),
			RetryCount = (int)dict["RetryCount"],
			LastRetryAt = dict["LastRetryAt"].HasValue ? DateTime.Parse(dict["LastRetryAt"].ToString()) : null,
			ErrorMessage = dict["ErrorMessage"].HasValue ? dict["ErrorMessage"].ToString() : null
		};
	}
}