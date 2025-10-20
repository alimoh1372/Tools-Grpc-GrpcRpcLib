using GrpcRpcLib.Shared.Entities.Models;
using GrpcRpcLib.Shared.MessageTools.DataBase.Models;
using GrpcRpcLib.Shared.MessageTools.Dtos;
using GrpcRpcLib.Shared.MessageTools.Dtos.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GrpcRpcLib.Shared.MessageTools.DataBase;
public class MessageDbContext : DbContext
{
	

	public MessageDbContext(
		DbContextOptions<MessageDbContext> options)
		: base(options)
	{
		
	}

	public DbSet<MessageEnvelope> Messages { get; set; }
	public DbSet<ServiceAddress> ServiceAddresses { get; set; }

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.ApplyConfiguration(new MessageEnvelopeConfiguration());
		modelBuilder.ApplyConfiguration(new ServiceAddressConfiguration());
	}
}