using GrpcRpcLib.Shared.Entities.Models;
using GrpcRpcLib.Shared.MessageTools.Abstraction;
using GrpcRpcLib.Shared.MessageTools.DataBase;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GrpcRpcLib.Shared.MessageTools.Implementation;

public class SqliteMessageStore : IMessageStore
{
	private readonly IServiceProvider _serviceProvider;

	public SqliteMessageStore(IServiceProvider serviceProvider)
	{
		_serviceProvider = serviceProvider;
	}

	public async Task SaveAsync(MessageEnvelope message, CancellationToken ct = default)
	{
		await using var scoper = _serviceProvider.CreateAsyncScope();
		await using var db = _serviceProvider.GetRequiredService<MessageDbContext>();

		await db.Messages.AddAsync(message, ct);
		await db.SaveChangesAsync(ct);
	}

	public async Task UpdateStatusAsync(Guid id, string status, CancellationToken ct = default)
	{
		await using var scoper = _serviceProvider.CreateAsyncScope();
		await using var db = _serviceProvider.GetRequiredService<MessageDbContext>();

		var msg = await db.Messages.FindAsync(new object[] { id }, ct);
		if (msg != null)
		{
			msg.Status = status;
			await db.SaveChangesAsync(ct);
		}
	}

	public async Task UpdateStatusAsync(Guid id, string status, string? errorMessage, CancellationToken ct = default)
	{
		await using var scoper = _serviceProvider.CreateAsyncScope();

		await using var db = _serviceProvider.GetRequiredService<MessageDbContext>();

		var msg = await db.Messages.FindAsync(new object[] { id }, ct);
		if (msg != null)
		{
			msg.Status = status;
			msg.ErrorMessage = errorMessage;
			await db.SaveChangesAsync(ct);
		}
	}

	public async Task<MessageEnvelope?> GetAsync(Guid id, CancellationToken ct = default)
	{
		await using var scoper = _serviceProvider.CreateAsyncScope();

		await using var db = _serviceProvider.GetRequiredService<MessageDbContext>();

		return await db.Messages.FindAsync(new object[] { id }, ct);
	}

	public async Task<IEnumerable<MessageEnvelope>> GetPendingAsync(CancellationToken ct = default)
	{
		await using var scoper = _serviceProvider.CreateAsyncScope();

		await using var db = _serviceProvider.GetRequiredService<MessageDbContext>();

		return await db.Messages
			.Where(m => m.Status == "ResponsePending")
			.ToListAsync(ct);
	}

	public async Task<IEnumerable<MessageEnvelope>> GetFailedAsync(CancellationToken ct = default)
	{
		await using var scoper = _serviceProvider.CreateAsyncScope();

		await using var db = _serviceProvider.GetRequiredService<MessageDbContext>();

		return await db.Messages
			.Where(m => m.Status == "Failed")
			.ToListAsync(ct);
	}

	public async Task<IEnumerable<MessageEnvelope>> GetRetryableAsync(CancellationToken ct = default)
	{
		await using var scoper = _serviceProvider.CreateAsyncScope();

		await using var db = _serviceProvider.GetRequiredService<MessageDbContext>();

		var cutoff = DateTime.UtcNow.AddMinutes(-5);

		return await db.Messages
			.Where(m => (m.Status == "Failed" || m.Status == "TryingToPublish") &&
						m.RetryCount < 3 &&
						(m.LastRetryAt == null || m.LastRetryAt < cutoff))
			.ToListAsync(ct);
	}

	public async Task IncrementRetryCountAsync(Guid id, CancellationToken ct = default)
	{
		await using var scoper = _serviceProvider.CreateAsyncScope();

		await using var db = _serviceProvider.GetRequiredService<MessageDbContext>();

		var msg = await db.Messages.FindAsync(new object[] { id }, ct);
		if (msg != null)
		{
			msg.RetryCount++;
			msg.LastRetryAt = DateTime.UtcNow;
			await db.SaveChangesAsync(ct);
		}
	}

	public async Task CleanupOldMessagesAsync(TimeSpan maxAge, CancellationToken ct = default)
	{

		var cutoff = DateTime.UtcNow.Subtract(maxAge);

		await using var scoper = _serviceProvider.CreateAsyncScope();

		await using var db = _serviceProvider.GetRequiredService<MessageDbContext>();

		var toRemove = await db.Messages
			.Where(m => m.CreatedAt < cutoff &&
						(m.Status == "Completed" || m.Status == "Failed" || m.Status == "TimedOut"))
			.ToListAsync(ct);
		db.Messages.RemoveRange(toRemove);
		await db.SaveChangesAsync(ct);
	}

	public async Task<Dictionary<string, int>> GetMessageCountsByStatusAsync(CancellationToken ct = default)
	{
		await using var scoper = _serviceProvider.CreateAsyncScope();

		await using var db = _serviceProvider.GetRequiredService<MessageDbContext>();

		var counts = await db.Messages
			.GroupBy(m => m.Status)
			.Select(g => new { Status = g.Key, Count = g.Count() })
			.ToDictionaryAsync(k => k.Status, v => v.Count, ct);
		return counts;
	}

	public async Task<IEnumerable<MessageEnvelope>> GetOldPendingMessagesAsync(TimeSpan maxAge, CancellationToken ct = default)
	{
		await using var scoper = _serviceProvider.CreateAsyncScope();

		await using var db = _serviceProvider.GetRequiredService<MessageDbContext>();

		var cutoff = DateTime.UtcNow.Subtract(maxAge);
		return await db.Messages
			.Where(m => m.Status == "ResponsePending" && m.CreatedAt < cutoff)
			.ToListAsync(ct);
	}

	public async Task<IEnumerable<MessageEnvelope>> LoadPendingMessagesAsync(CancellationToken ct = default)
	{
		await using var scope = _serviceProvider.CreateAsyncScope();

		await using var db = _serviceProvider.GetRequiredService<MessageDbContext>();

		return await db.Messages
			.Where(m => m.Status != "Completed" && m.Status != "TimedOut")
			.ToListAsync(ct);
	}
}