using GrpcRpcLib.Shared.Entiteis.Models;

namespace GrpcRpcLib.Shared.MessageTools.Abstraction;

public interface IMessageStore
{
	Task SaveAsync(MessageEnvelope message, CancellationToken ct = default);
	Task UpdateStatusAsync(Guid id, string status, CancellationToken ct = default);
	Task UpdateStatusAsync(Guid id, string status, string? errorMessage, CancellationToken ct = default);
	Task<MessageEnvelope?> GetAsync(Guid id, CancellationToken ct = default);
	Task<IEnumerable<MessageEnvelope>> GetPendingAsync(CancellationToken ct = default);
	Task<IEnumerable<MessageEnvelope>> GetFailedAsync(CancellationToken ct = default);
	Task<IEnumerable<MessageEnvelope>> GetRetryableAsync(CancellationToken ct = default);
	Task IncrementRetryCountAsync(Guid id, CancellationToken ct = default);
	Task CleanupOldMessagesAsync(TimeSpan maxAge, CancellationToken ct = default);
	Task<Dictionary<string, int>> GetMessageCountsByStatusAsync(CancellationToken ct = default);
	Task<IEnumerable<MessageEnvelope>> GetOldPendingMessagesAsync(TimeSpan maxAge, CancellationToken ct = default);
	Task<IEnumerable<MessageEnvelope>> LoadPendingMessagesAsync(CancellationToken ct = default);
}