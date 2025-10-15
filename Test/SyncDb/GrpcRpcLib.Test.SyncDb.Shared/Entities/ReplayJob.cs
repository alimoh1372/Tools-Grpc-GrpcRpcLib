namespace GrpcRpcLib.Test.SyncDb.Shared.Entities;

public class ReplayJob
{
	public long JobId { get; set; }
	public string RequestorService { get; set; } = "";
	public string AggregateType { get; set; } = "";
	public int AggregateId { get; set; }
	public long FromSequence { get; set; }
	public long ToSequence { get; set; }
	public string Status { get; set; } = "Pending";
	public int Attempts { get; set; } = 0;
	public string? LastError { get; set; }
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	public DateTime? LastRunAt { get; set; }
}