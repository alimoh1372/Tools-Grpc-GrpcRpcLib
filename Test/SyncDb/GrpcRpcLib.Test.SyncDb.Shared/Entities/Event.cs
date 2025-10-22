namespace GrpcRpcLib.Test.SyncDb.Shared.Entities;

public class Event
{
	public Guid EventId { get; set; } = Guid.NewGuid();
	public int Priority { get; set; } = 10;
	public string AggregateType { get; set; } = "";
	public int AggregateId { get; set; }
	public long SequenceNumber { get; set; } = 0;
	public string EventType { get; set; } = "";
	public byte[] Payload { get; set; } = [];
	public string Status { get; set; } = "Pending"; // Pending, Processing, PublishedToOtherServiceCompletely, Failed
	public int Attempts { get; set; } = 0;
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	public DateTime? LastAttemptAt { get; set; }
	public string ProcessorInstanceId { get; set; }=Guid.NewGuid().ToString();
}