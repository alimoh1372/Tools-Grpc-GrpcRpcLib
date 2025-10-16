using GrpcRpcLib.Publisher.Configurations;
using GrpcRpcLib.Shared.MessageTools.DataBase;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace GrpcRpcLib.Publisher.Services;

public class AddressResolver(
	IOptionsMonitor<GrpcPublisherConfiguration> configMonitor,
	MessageDbContext dbContext,
	IMemoryCache cache)
{
	private string? _cachedTargetHost;
	private string? _cachedReplyTo;

	public async Task<string> GetTargetHostAsync(bool forceRefresh = false)
	{
		var config = configMonitor.CurrentValue;
		var cacheKey = $"TargetHost_{config.TargetHostId}";

		if (forceRefresh || !cache.TryGetValue(cacheKey, out string? cachedValue))
		{
			var serviceAddress = await dbContext.ServiceAddresses
				.FirstOrDefaultAsync(s => s.ServiceName == config.TargetHostId);

			_cachedTargetHost = serviceAddress?.Address ?? throw new InvalidOperationException($"TargetHost with ID {config.TargetHostId} not found in DB.");

			cache.Set(cacheKey, _cachedTargetHost, TimeSpan.FromMinutes(10));
		}
		else
		{
			_cachedTargetHost = cachedValue;
		}

		return _cachedTargetHost!;
	}

	public async Task<string> GetTargetHostByIdAsync(string id, bool forceRefresh = false)
	{
		
		var cacheKey = $"TargetHost_{id}";

		if (forceRefresh || !cache.TryGetValue(cacheKey, out string? cachedValue))
		{

			var serviceAddress = await dbContext.ServiceAddresses
				.FirstOrDefaultAsync(s => s.ServiceName == cacheKey);

			cachedValue= serviceAddress?.Address ?? throw new InvalidOperationException($"TargetHost with ID {cacheKey} not found in DB.");

			cache.Set(cacheKey, cachedValue, TimeSpan.FromMinutes(10));

		}

		return cachedValue!;

	}

	public async Task<string> GetReplyToAsync(bool forceRefresh = false)
	{
		var config = configMonitor.CurrentValue;
		var cacheKey = $"ReplyTo_{config.ReplyToId}";

		if (forceRefresh || !cache.TryGetValue(cacheKey, out string? cachedValue))
		{
			var serviceAddress = await dbContext.ServiceAddresses
				.FirstOrDefaultAsync(s => s.ServiceName == config.ReplyToId);

			_cachedReplyTo = serviceAddress?.Address ?? throw new InvalidOperationException($"ReplyTo with ID {config.ReplyToId} not found in DB.");

			cache.Set(cacheKey, _cachedReplyTo, TimeSpan.FromMinutes(10));
		}
		else
		{
			_cachedReplyTo = cachedValue;
		}

		return _cachedReplyTo;
	}

	public void InvalidateCache()
	{
		var config = configMonitor.CurrentValue;
		cache.Remove($"TargetHost_{config.TargetHostId}");
		cache.Remove($"ReplyTo_{config.ReplyToId}");
	}
}