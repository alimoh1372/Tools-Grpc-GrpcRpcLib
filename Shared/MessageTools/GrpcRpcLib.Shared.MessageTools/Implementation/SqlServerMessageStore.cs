using GrpcRpcLib.Shared.Entities.Models;
using GrpcRpcLib.Shared.MessageTools.Abstraction;
using GrpcRpcLib.Shared.MessageTools.DataBase;
using Microsoft.EntityFrameworkCore;

namespace GrpcRpcLib.Shared.MessageTools.Implementation;

public class SqlServerMessageStore(MessageDbContext dbContext, string prefix = "") : IMessageStore
{
	private readonly MessageDbContext _db = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
	private readonly string _prefix = prefix;

	public async Task SaveAsync(MessageEnvelope message, CancellationToken ct = default)
	{
		await _db.Messages.AddAsync(message, ct);
		await _db.SaveChangesAsync(ct);
	}

	public async Task UpdateStatusAsync(Guid id, string status, CancellationToken ct = default)
	{
		var msg = await _db.Messages.FindAsync(new object[] { id }, ct);
		if (msg != null)
		{
			msg.Status = status;
			await _db.SaveChangesAsync(ct);
		}
	}

	public async Task UpdateStatusAsync(Guid id, string status, string? errorMessage, CancellationToken ct = default)
	{
		var msg = await _db.Messages.FindAsync(new object[] { id }, ct);
		if (msg != null)
		{
			msg.Status = status;
			msg.ErrorMessage = errorMessage;
			await _db.SaveChangesAsync(ct);
		}
	}

	public async Task<MessageEnvelope?> GetAsync(Guid id, CancellationToken ct = default)
	{
		return await _db.Messages.FindAsync(new object[] { id }, ct);
	}

	public async Task<IEnumerable<MessageEnvelope>> GetPendingAsync(CancellationToken ct = default)
	{
		return await _db.Messages
			.Where(m => m.Status == "ResponsePending")
			.ToListAsync(ct);
	}

	public async Task<IEnumerable<MessageEnvelope>> GetFailedAsync(CancellationToken ct = default)
	{
		return await _db.Messages
			.Where(m => m.Status == "Failed")
			.ToListAsync(ct);
	}

	public async Task<IEnumerable<MessageEnvelope>> GetRetryableAsync(CancellationToken ct = default)
	{
		var cutoff = DateTime.UtcNow.AddMinutes(-5);
		return await _db.Messages
			.Where(m => (m.Status == "Failed" || m.Status == "TryingToPublish") &&
						m.RetryCount < 3 &&
						(m.LastRetryAt == null || m.LastRetryAt < cutoff))
			.ToListAsync(ct);
	}

	public async Task IncrementRetryCountAsync(Guid id, CancellationToken ct = default)
	{
		var msg = await _db.Messages.FindAsync(new object[] { id }, ct);
		if (msg != null)
		{
			msg.RetryCount++;
			msg.LastRetryAt = DateTime.UtcNow;
			await _db.SaveChangesAsync(ct);
		}
	}

	public async Task CleanupOldMessagesAsync(TimeSpan maxAge, CancellationToken ct = default)
	{
		var cutoff = DateTime.UtcNow.Subtract(maxAge);
		var toRemove = await _db.Messages
			.Where(m => m.CreatedAt < cutoff &&
						(m.Status == "Completed" || m.Status == "Failed" || m.Status == "TimedOut"))
			.ToListAsync(ct);
		_db.Messages.RemoveRange(toRemove);
		await _db.SaveChangesAsync(ct);
	}

	public async Task<Dictionary<string, int>> GetMessageCountsByStatusAsync(CancellationToken ct = default)
	{
		var counts = await _db.Messages
			.GroupBy(m => m.Status)
			.Select(g => new { Status = g.Key, Count = g.Count() })
			.ToDictionaryAsync(k => k.Status, v => v.Count, ct);
		return counts;
	}

	public async Task<IEnumerable<MessageEnvelope>> GetOldPendingMessagesAsync(TimeSpan maxAge, CancellationToken ct = default)
	{
		var cutoff = DateTime.UtcNow.Subtract(maxAge);
		return await _db.Messages
			.Where(m => m.Status == "ResponsePending" && m.CreatedAt < cutoff)
			.ToListAsync(ct);
	}

	public async Task<IEnumerable<MessageEnvelope>> LoadPendingMessagesAsync(CancellationToken ct = default)
	{
		return await _db.Messages
			.Where(m => m.Status != "Completed" && m.Status != "TimedOut")
			.ToListAsync(ct);
	}
}
