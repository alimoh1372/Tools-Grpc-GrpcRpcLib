using System.Collections.Concurrent;
using GrpcRpcLib.Shared.Entities.Models;
using GrpcRpcLib.Shared.MessageTools.Abstraction;

namespace GrpcRpcLib.Shared.MessageTools.Implementation;
public class InMemoryMessageStore : IMessageStore
{
	private readonly ConcurrentDictionary<Guid, MessageEnvelope> _messages = new();

	public async Task SaveAsync(MessageEnvelope message, CancellationToken ct = default)
	{
		_messages[message.Id] = message;
		await Task.CompletedTask;
	}

	public async Task UpdateStatusAsync(Guid id, string status, CancellationToken ct = default)
	{
		if (_messages.TryGetValue(id, out var msg))
			msg.Status = status;
		await Task.CompletedTask;
	}

	public async Task UpdateStatusAsync(Guid id, string status, string? errorMessage, CancellationToken ct = default)
	{
		if (_messages.TryGetValue(id, out var msg))
		{
			msg.Status = status;
			msg.ErrorMessage = errorMessage;
		}
		await Task.CompletedTask;
	}

	public async Task<MessageEnvelope?> GetAsync(Guid id, CancellationToken ct = default)
		=> await Task.FromResult(_messages.TryGetValue(id, out var msg) ? msg : null);

	public async Task<IEnumerable<MessageEnvelope>> GetPendingAsync(CancellationToken ct = default)
		=> await Task.FromResult(_messages.Values.Where(m => m.Status == "ResponsePending").AsEnumerable());

	public async Task<IEnumerable<MessageEnvelope>> GetFailedAsync(CancellationToken ct = default)
		=> await Task.FromResult(_messages.Values.Where(m => m.Status == "Failed").AsEnumerable());

	public async Task<IEnumerable<MessageEnvelope>> GetRetryableAsync(CancellationToken ct = default)
	{
		var cutoff = DateTime.UtcNow.AddMinutes(-5);
		return await Task.FromResult(_messages.Values.Where(m =>
			(m.Status == "Failed" || m.Status == "TryingToPublish") &&
			m.RetryCount < 3 &&
			(m.LastRetryAt == null || m.LastRetryAt < cutoff)).AsEnumerable());
	}

	public async Task IncrementRetryCountAsync(Guid id, CancellationToken ct = default)
	{
		if (_messages.TryGetValue(id, out var msg))
		{
			msg.RetryCount++;
			msg.LastRetryAt = DateTime.UtcNow;
		}
		await Task.CompletedTask;
	}

	public async Task CleanupOldMessagesAsync(TimeSpan maxAge, CancellationToken ct = default)
	{
		var cutoff = DateTime.UtcNow.Subtract(maxAge);
		var toRemove = _messages.Values.Where(m => m.CreatedAt < cutoff &&
			(m.Status == "Completed" || m.Status == "Failed" || m.Status == "TimedOut")).ToList();
		foreach (var msg in toRemove)
			_messages.TryRemove(msg.Id, out _);
		await Task.CompletedTask;
	}

	public async Task<Dictionary<string, int>> GetMessageCountsByStatusAsync(CancellationToken ct = default)
	{
		var counts = _messages.Values.GroupBy(m => m.Status).ToDictionary(g => g.Key, g => g.Count());
		return await Task.FromResult(counts);
	}

	public async Task<IEnumerable<MessageEnvelope>> GetOldPendingMessagesAsync(TimeSpan maxAge, CancellationToken ct = default)
	{
		var cutoff = DateTime.UtcNow.Subtract(maxAge);
		return await Task.FromResult(_messages.Values.Where(m => m.Status == "ResponsePending" && m.CreatedAt < cutoff).AsEnumerable());
	}

	public async Task<IEnumerable<MessageEnvelope>> LoadPendingMessagesAsync(CancellationToken ct = default)
		=> await Task.FromResult(_messages.Values.Where(m => m.Status != "Completed" && m.Status != "TimedOut").AsEnumerable());
}