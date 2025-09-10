using GrpcRpcLib.Shared.Entities.Models;
using GrpcRpcLib.Shared.MessageTools.DataBase.Models;
using GrpcRpcLib.Shared.MessageTools.Dtos;
using Microsoft.EntityFrameworkCore;

namespace GrpcRpcLib.Shared.MessageTools.DataBase;

public class MessageDbContext(DbContextOptions<MessageDbContext> options, string prefix = "")
	: DbContext(options)
{
	private readonly string _prefix = prefix ?? "";

	public DbSet<MessageEnvelope> Messages { get; set; }
	public DbSet<ServiceAddress> ServiceAddresses { get; set; }

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.ApplyConfiguration(new MessageEnvelopeConfiguration(_prefix));
		modelBuilder.ApplyConfiguration(new ServiceAddressConfiguration(_prefix));
	}
}