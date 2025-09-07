namespace GrpcRpcLib.Shared.MessageTools;

public static class DateTimeExtensions
{
	private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

	public static long ToUnixTimeSeconds(this DateTime dateTime)
	{
		return (long)(dateTime.ToUniversalTime() - UnixEpoch).TotalSeconds;
	}

	public static DateTime FromUnixTimeSeconds(this long unixSeconds)
	{
		return UnixEpoch.AddSeconds(unixSeconds);
	}
}