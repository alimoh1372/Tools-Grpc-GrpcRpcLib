using GrpcRpcLib.Shared.Entities.Models;
using GrpcRpcLib.Shared.MessageTools.Abstraction;
using GrpcRpcLib.Shared.MessageTools.DataBase;
using Microsoft.EntityFrameworkCore;

namespace GrpcRpcLib.Shared.MessageTools.Implementation;

public class SqliteMessageStore : IMessageStore
{
	private readonly DbContextOptions<MessageDbContext> _options;

	public SqliteMessageStore(string connectionString)
	{
		_options = new DbContextOptionsBuilder<MessageDbContext>()
			.UseSqlite(connectionString)
			.Options;

		using var context = new MessageDbContext(_options);
		context.Database.EnsureCreated();
	}

	public async Task SaveAsync(MessageEnvelope message, CancellationToken ct = default)
	{
		await using var context = new MessageDbContext(_options);

		context.Messages.Add(message);
		await context.SaveChangesAsync(ct);
	}

	public async Task UpdateStatusAsync(Guid id, string status, CancellationToken ct = default)
	{
		await using var context = new MessageDbContext(_options);
		var message = await context.Messages.FindAsync(new object[] { id }, ct);
		if (message != null)
		{
			message.Status = status;
			await context.SaveChangesAsync(ct);
		}
	}

	public async Task UpdateStatusAsync(Guid id, string status, string? errorMessage, CancellationToken ct = default)
	{
		using var context = new MessageDbContext(_options);
		var message = await context.Messages.FindAsync(new object[] { id }, ct);
		if (message != null)
		{
			message.Status = status;
			message.ErrorMessage = errorMessage;
			await context.SaveChangesAsync(ct);
		}
	}

	public async Task<MessageEnvelope?> GetAsync(Guid id, CancellationToken ct = default)
	{
		using var context = new MessageDbContext(_options);
		return await context.Messages.FindAsync(new object[] { id }, ct);
	}

	public async Task<IEnumerable<MessageEnvelope>> GetPendingAsync(CancellationToken ct = default)
	{
		using var context = new MessageDbContext(_options);
		return await context.Messages
			.Where(m => m.Status == "ResponsePending")
			.ToListAsync(ct);
	}

	public async Task<IEnumerable<MessageEnvelope>> GetFailedAsync(CancellationToken ct = default)
	{
		using var context = new MessageDbContext(_options);
		return await context.Messages
			.Where(m => m.Status == "Failed")
			.ToListAsync(ct);
	}

	public async Task<IEnumerable<MessageEnvelope>> GetRetryableAsync(CancellationToken ct = default)
	{
		var cutoff = DateTime.UtcNow.AddMinutes(-5);
		using var context = new MessageDbContext(_options);
		return await context.Messages
			.Where(m => (m.Status == "Failed" || m.Status == "TryingToPublish") &&
						m.RetryCount < 3 &&
						(m.LastRetryAt == null || m.LastRetryAt < cutoff))
			.ToListAsync(ct);
	}

	public async Task IncrementRetryCountAsync(Guid id, CancellationToken ct = default)
	{
		using var context = new MessageDbContext(_options);
		var message = await context.Messages.FindAsync(new object[] { id }, ct);
		if (message != null)
		{
			message.RetryCount++;
			message.LastRetryAt = DateTime.UtcNow;
			await context.SaveChangesAsync(ct);
		}
	}

	public async Task CleanupOldMessagesAsync(TimeSpan maxAge, CancellationToken ct = default)
	{
		var cutoff = DateTime.UtcNow.Subtract(maxAge);
		using var context = new MessageDbContext(_options);
		var messages = await context.Messages
			.Where(m => m.CreatedAt < cutoff &&
						(m.Status == "Completed" || m.Status == "Failed" || m.Status == "TimedOut"))
			.ToListAsync(ct);
		context.Messages.RemoveRange(messages);
		await context.SaveChangesAsync(ct);
	}

	public async Task<Dictionary<string, int>> GetMessageCountsByStatusAsync(CancellationToken ct = default)
	{
		using var context = new MessageDbContext(_options);
		var counts = await context.Messages
			.GroupBy(m => m.Status)
			.Select(g => new { Status = g.Key, Count = g.Count() })
			.ToDictionaryAsync(k => k.Status, v => v.Count, ct);
		return counts;
	}

	public async Task<IEnumerable<MessageEnvelope>> GetOldPendingMessagesAsync(TimeSpan maxAge, CancellationToken ct = default)
	{
		var cutoff = DateTime.UtcNow.Subtract(maxAge);
		using var context = new MessageDbContext(_options);
		return await context.Messages
			.Where(m => m.Status == "ResponsePending" && m.CreatedAt < cutoff)
			.ToListAsync(ct);
	}

	public async Task<IEnumerable<MessageEnvelope>> LoadPendingMessagesAsync(CancellationToken ct = default)
	{
		using var context = new MessageDbContext(_options);
		return await context.Messages
			.Where(m => m.Status != "Completed" && m.Status != "TimedOut")
			.ToListAsync(ct);
	}
}