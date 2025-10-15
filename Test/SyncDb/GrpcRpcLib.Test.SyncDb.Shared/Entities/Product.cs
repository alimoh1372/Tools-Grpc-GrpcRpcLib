namespace GrpcRpcLib.Test.SyncDb.Shared.Entities;

public class Product
{
	public int Id { get; set; }               // دستی مقدار می‌دهیم
	public string Sku { get; set; } = "";
	public string Title { get; set; } = "";
}