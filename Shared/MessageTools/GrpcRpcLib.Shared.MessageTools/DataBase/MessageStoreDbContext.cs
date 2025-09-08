using GrpcRpcLib.Shared.Entities.Models;
using GrpcRpcLib.Shared.MessageTools.DataBase.Models;
using GrpcRpcLib.Shared.MessageTools.Dtos;
using Microsoft.EntityFrameworkCore;

namespace GrpcRpcLib.Shared.MessageTools.DataBase;

public class MessageStoreDbContext(DbContextOptions<MessageStoreDbContext> options, string prefix = "")
	: DbContext(options)
{
	public DbSet<MessageEnvelope> MessageEnvelopes { get; set; }
	public DbSet<ServiceAddress> ServiceAddresses { get; set; }

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.ApplyConfiguration(new MessageEnvelopeConfiguration(prefix));
		modelBuilder.ApplyConfiguration(new ServiceAddressConfiguration(prefix));
		base.OnModelCreating(modelBuilder);
	}
}