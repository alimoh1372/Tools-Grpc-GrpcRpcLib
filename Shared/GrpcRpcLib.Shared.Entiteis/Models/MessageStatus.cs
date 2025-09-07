namespace GrpcRpcLib.Shared.Entiteis.Models;

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