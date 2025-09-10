namespace GrpcRpcLib.Shared.MessageTools.Dtos.Configurations;

public class MessageStoreConfiguration
{
	public const string SectionName = "MessageStoreConfiguration";
	public string StorageType { get; set; } = "InMemory";

	public string StorageConnectionString { get; set; } = "Data Source=messages.db";
	public string StoragePrefix { get; set; } = "";
}

