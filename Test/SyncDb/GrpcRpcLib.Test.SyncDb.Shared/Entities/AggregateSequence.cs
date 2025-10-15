namespace GrpcRpcLib.Test.SyncDb.Shared.Entities;

public class AggregateSequence
{
	public string AggregateType { get; set; } = "";
	public int AggregateId { get; set; }
	public long LastSequence { get; set; } = 0;
}