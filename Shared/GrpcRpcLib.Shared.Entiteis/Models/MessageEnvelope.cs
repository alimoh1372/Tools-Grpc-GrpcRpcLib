namespace GrpcRpcLib.Shared.Entities.Models;

public class MessageEnvelope
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public string Type { get; set; } = string.Empty;
	public string CorrelationId { get; set; } = string.Empty;
	public int Priority { get; set; } = 0; // 0=low, higher=better
	public string ReplyTo { get; set; } = "";
	public byte[] Payload { get; set; } = [];
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	public string Status { get; set; } = "TryingToPublish";
	public int RetryCount { get; set; } = 0;
	public DateTime? LastRetryAt { get; set; }
	public string? ErrorMessage { get; set; }
}