using GrpcRpcLib.Shared.Entities.Models;
using GrpcRpcLib.Shared.MessageTools.DataBase.Models;
using GrpcRpcLib.Shared.MessageTools.Dtos;
using GrpcRpcLib.Shared.MessageTools.Dtos.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GrpcRpcLib.Shared.MessageTools.DataBase;
public class MessageDbContext : DbContext
{
	private readonly string _prefix;

	public MessageDbContext(
		DbContextOptions<MessageDbContext> options, 
		IOptions<MessageStoreConfiguration> cfg)
		: base(options)
	{
		// اگر cfg null بود prefix خالی خواهد بود
		_prefix = cfg?.Value?.StoragePrefix ?? string.Empty;
	}

	public DbSet<MessageEnvelope> Messages { get; set; }
	public DbSet<ServiceAddress> ServiceAddresses { get; set; }

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.ApplyConfiguration(new MessageEnvelopeConfiguration(_prefix));
		modelBuilder.ApplyConfiguration(new ServiceAddressConfiguration(_prefix));
	}
}