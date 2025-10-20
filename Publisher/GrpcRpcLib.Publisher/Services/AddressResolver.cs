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
	public async Task<string> GetHostByIdAsync(string id, bool forceRefresh = false)
	{
		if (forceRefresh || !cache.TryGetValue(id, out string? cachedValue))
		{

			var serviceAddress = await dbContext.ServiceAddresses
				.FirstOrDefaultAsync(s => s.ServiceName == id);

			cachedValue= serviceAddress?.Address ?? throw new InvalidOperationException($"TargetHost with ID {id} not found in DB.");

			cache.Set(id, cachedValue, TimeSpan.FromMinutes(10));

		}

		return cachedValue!;

	}

	
}