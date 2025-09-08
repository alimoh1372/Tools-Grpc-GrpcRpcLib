namespace GrpcRpcLib.Shared.Entities.Models;

public enum MessageStatus
{
	TryingToPublish,
	Publishing,
	ResponsePending,
	Failed,
	Completed,
	Retrying,
	Received,
	TimedOut
}