namespace GrpcRpcLib.Publisher.Configurations;

public class GrpcPublisherConfiguration
{
	public const string SectionName = "GrpcPublisherConfiguration";
	public string TargetHostId { get; set; } = "21";
	public string ReplyToId { get; set; } = "13";
	public TimeSpan TimeoutDuration { get; set; } = TimeSpan.FromMinutes(5);
	public int MaxRetryCount { get; set; } = 3;
	public string StorageConnectionString { get; set; } = "Data Source=messages.db";
	public string StorageType { get; set; } = "InMemory";
	public string StoragePrefix { get; set; } = "";
}