namespace GrpcRpcLib.Publisher.Configurations;

public class GrpcPublisherConfiguration
{
	public const string SectionName = "GrpcPublisherConfiguration";
	public string TargetHostId { get; set; } = "21";
	public string ReplyToId { get; set; } = "13";
	public int TimeoutDurationInSeconds { get; set; } = 5;
	public int MaxRetryCount { get; set; } = 3;
	
}