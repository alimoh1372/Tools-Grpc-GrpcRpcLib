namespace GrpcRpcLib.Test.SyncDb.Shared.Entities;

public class User
{
	public int Id { get; set; }               // دستی مقدار می‌دهیم
	public string Username { get; set; } = "";
	public string FullName { get; set; } = "";
}