namespace GrpcRpcLib.Consumer.Dtos.Configurations;

public class GrpcRpcConsumerConfiguration
{
	public GrpcRpcConsumerConfiguration()
	{
	}

	public GrpcRpcConsumerConfiguration(string host, int port)
	{
		Host = host;
		Port = port;
	}

	public const string SectionName = "GrpcRpcConsumerConfiguration";
	public string Host { get; set; } = "0.0.0.0";
	public int Port { get; set; } = 5001;
	public int TimeoutSeconds { get; set; } = 30;
}