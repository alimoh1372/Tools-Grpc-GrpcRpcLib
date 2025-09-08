using GrpcRpcLib.Shared.Entities.Models;
using GrpcRpcLib.Shared.MessageTools.Abstraction;
using GrpcRpcLib.Shared.MessageTools.DataBase;
using Microsoft.EntityFrameworkCore;

namespace GrpcRpcLib.Shared.MessageTools.Implementation;

public class SqlServerMessageStore(MessageStoreDbContext dbContext) : IMessageStore
{
	public async Task SaveAsync(MessageEnvelope message, CancellationToken ct = default)
	{
		dbContext.MessageEnvelopes.Add(message);
		await dbContext.SaveChangesAsync(ct);
	}

	public async Task UpdateStatusAsync(Guid id, string status, CancellationToken ct = default)
	{
		var message = await dbContext.MessageEnvelopes.FindAsync(new object[] { id }, ct);
		if (message != null)
		{
			message.Status = status;
			await dbContext.SaveChangesAsync(ct);
		}
	}

	public async Task UpdateStatusAsync(Guid id, string status, string? errorMessage, CancellationToken ct = default)
	{
		var message = await dbContext.MessageEnvelopes.FindAsync(new object[] { id }, ct);
		if (message != null)
		{
			message.Status = status;
			message.ErrorMessage = errorMessage;
			await dbContext.SaveChangesAsync(ct);
		}
	}

	public async Task<MessageEnvelope?> GetAsync(Guid id, CancellationToken ct = default)
	{
		return await dbContext.MessageEnvelopes.FindAsync(new object[] { id }, ct);
	}

	public async Task<IEnumerable<MessageEnvelope>> GetPendingAsync(CancellationToken ct = default)
	{
		return await dbContext.MessageEnvelopes
			.Where(m => m.Status == "Pending")
			.ToListAsync(ct);
	}

	public async Task<IEnumerable<MessageEnvelope>> GetFailedAsync(CancellationToken ct = default)
	{
		return await dbContext.MessageEnvelopes
			.Where(m => m.Status == "Failed")
			.ToListAsync(ct);
	}

	public async Task<IEnumerable<MessageEnvelope>> GetRetryableAsync(CancellationToken ct = default)
	{
		return await dbContext.MessageEnvelopes
			.Where(m => m.Status == "Retryable")
			.ToListAsync(ct);
	}

	public async Task IncrementRetryCountAsync(Guid id, CancellationToken ct = default)
	{
		var message = await dbContext.MessageEnvelopes.FindAsync(new object[] { id }, ct);
		if (message != null)
		{
			message.RetryCount++;
			message.LastRetryAt = DateTime.UtcNow;
			await dbContext.SaveChangesAsync(ct);
		}
	}

	public async Task CleanupOldMessagesAsync(TimeSpan maxAge, CancellationToken ct = default)
	{
		var cutoff = DateTime.UtcNow - maxAge;
		var oldMessages = await dbContext.MessageEnvelopes
			.Where(m => m.CreatedAt < cutoff)
			.ToListAsync(ct);
		dbContext.MessageEnvelopes.RemoveRange(oldMessages);
		await dbContext.SaveChangesAsync(ct);
	}

	public async Task<Dictionary<string, int>> GetMessageCountsByStatusAsync(CancellationToken ct = default)
	{
		return await dbContext.MessageEnvelopes
			.GroupBy(m => m.Status)
			.Select(g => new { Status = g.Key, Count = g.Count() })
			.ToDictionaryAsync(x => x.Status, x => x.Count, ct);
	}

	public async Task<IEnumerable<MessageEnvelope>> GetOldPendingMessagesAsync(TimeSpan maxAge, CancellationToken ct = default)
	{
		var cutoff = DateTime.UtcNow - maxAge;
		return await dbContext.MessageEnvelopes
			.Where(m => m.Status == "Pending" && m.CreatedAt < cutoff)
			.ToListAsync(ct);
	}

	public async Task<IEnumerable<MessageEnvelope>> LoadPendingMessagesAsync(CancellationToken ct = default)
	{
		return await GetPendingAsync(ct);
	}
}