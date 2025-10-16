namespace GrpcRpcLib.Shared.MessageTools.Dtos;

public class ServiceAddress
{
	public int Id { get; set; }
	public string ServiceName { get; set; } = string.Empty;
	public string Address { get; set; } = string.Empty;
	public bool CurrentService { get; set; }=false;
}