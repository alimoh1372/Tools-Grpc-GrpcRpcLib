using GrpcRpcLib.Shared.Entities.Models;
using GrpcRpcLib.Shared.MessageTools.Abstraction;
using Newtonsoft.Json;

namespace GrpcRpcLib.Shared.MessageTools.Implementation;

public class FileMessageStore : IMessageStore
{
	private readonly string _filePath;
	private readonly SemaphoreSlim _semaphore = new(1, 1);

	public FileMessageStore(string filePath)
	{
		_filePath = filePath;
		Directory.CreateDirectory(Path.GetDirectoryName(_filePath) ?? "");
	}

	public async Task SaveAsync(MessageEnvelope message, CancellationToken ct = default)
	{
		await _semaphore.WaitAsync(ct);
		try
		{
			var messages = await LoadAllMessages();
			messages[message.Id] = message;
			await SaveAllMessages(messages);
		}
		finally
		{
			_semaphore.Release();
		}
	}

	public async Task UpdateStatusAsync(Guid id, string status, CancellationToken ct = default)
	{
		await _semaphore.WaitAsync(ct);
		try
		{
			var messages = await LoadAllMessages();
			if (messages.TryGetValue(id, out var message))
			{
				message.Status = status;
				await SaveAllMessages(messages);
			}
		}
		finally
		{
			_semaphore.Release();
		}
	}

	public async Task UpdateStatusAsync(Guid id, string status, string? errorMessage, CancellationToken ct = default)
	{
		await _semaphore.WaitAsync(ct);
		try
		{
			var messages = await LoadAllMessages();
			if (messages.TryGetValue(id, out var message))
			{
				message.Status = status;
				message.ErrorMessage = errorMessage;
				await SaveAllMessages(messages);
			}
		}
		finally
		{
			_semaphore.Release();
		}
	}


	public async Task<MessageEnvelope?> GetAsync(Guid id, CancellationToken ct = default)
	{
		var messages = await LoadAllMessages();
		return messages.GetValueOrDefault(id);
	}

	public async Task<IEnumerable<MessageEnvelope>> GetPendingAsync(CancellationToken ct = default)
	{
		var messages = await LoadAllMessages();
		return messages.Values.Where(m => m.Status == MessageStatus.ResponsePending.ToString());
	}

	public async Task<IEnumerable<MessageEnvelope>> GetFailedAsync(CancellationToken ct = default)
	{
		var messages = await LoadAllMessages();
		return messages.Values.Where(m => m.Status == MessageStatus.Failed.ToString());
	}

	public async Task<IEnumerable<MessageEnvelope>> GetRetryableAsync(CancellationToken ct = default)
	{
		var messages = await LoadAllMessages();
		var cutoff = DateTime.UtcNow.AddMinutes(-5);
		return messages.Values.Where(m =>
			(m.Status == MessageStatus.Failed.ToString() || m.Status == MessageStatus.TryingToPublish.ToString()) &&
			m.RetryCount < 3 &&
			(m.LastRetryAt == null || m.LastRetryAt < cutoff));
	}

	public async Task IncrementRetryCountAsync(Guid id, CancellationToken ct = default)
	{
		await _semaphore.WaitAsync(ct);
		try
		{
			var messages = await LoadAllMessages();
			if (messages.TryGetValue(id, out var message))
			{
				message.RetryCount++;
				message.LastRetryAt = DateTime.UtcNow;
				await SaveAllMessages(messages);
			}
		}
		finally
		{
			_semaphore.Release();
		}
	}

	public async Task CleanupOldMessagesAsync(TimeSpan maxAge, CancellationToken ct = default)
	{
		await _semaphore.WaitAsync(ct);
		try
		{
			var messages = await LoadAllMessages();
			var cutoff = DateTime.UtcNow.Subtract(maxAge);
			var toRemove = messages.Values.Where(m => m.CreatedAt < cutoff &&
				(m.Status == MessageStatus.Completed.ToString() || m.Status == MessageStatus.Failed.ToString() || m.Status == MessageStatus.TimedOut.ToString())).ToList();
			foreach (var msg in toRemove)
				messages.Remove(msg.Id);
			await SaveAllMessages(messages);
		}
		finally
		{
			_semaphore.Release();
		}
	}

	async Task<Dictionary<string, int>> IMessageStore.GetMessageCountsByStatusAsync(CancellationToken ct)
	{
		var messages = await LoadAllMessages();
		var counts = messages.Values.GroupBy(m => m.Status).ToDictionary(g => g.Key, g => g.Count());
		return counts;
	}

	

	public async Task<IEnumerable<MessageEnvelope>> GetOldPendingMessagesAsync(TimeSpan maxAge, CancellationToken ct = default)
	{
		var messages = await LoadAllMessages();
		var cutoff = DateTime.UtcNow.Subtract(maxAge);
		return messages.Values.Where(m => m.Status == MessageStatus.ResponsePending.ToString() && m.CreatedAt < cutoff);
	}

	public async Task<IEnumerable<MessageEnvelope>> LoadPendingMessagesAsync(CancellationToken ct = default)
	{
		var messages = await LoadAllMessages();
		
		return messages.Values.Where(m => m.Status == MessageStatus.ResponsePending.ToString());
	}

	private async Task<Dictionary<Guid, MessageEnvelope>> LoadAllMessages()
	{
		if (!File.Exists(_filePath))
			return new Dictionary<Guid, MessageEnvelope>();

		var json = await File.ReadAllTextAsync(_filePath);
		var messages = JsonConvert.DeserializeObject<Dictionary<Guid, MessageEnvelope>>(json);
		return messages ?? new Dictionary<Guid, MessageEnvelope>();
	}

	private async Task SaveAllMessages(Dictionary<Guid, MessageEnvelope> messages)
	{
		var json = JsonConvert.SerializeObject(messages,Newtonsoft.Json.Formatting.Indented);

		await File.WriteAllTextAsync(_filePath, json);
	}
}