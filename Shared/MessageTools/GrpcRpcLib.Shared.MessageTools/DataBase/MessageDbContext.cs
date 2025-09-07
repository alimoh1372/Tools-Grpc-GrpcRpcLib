using GrpcRpcLib.Shared.Entiteis.Models;
using Microsoft.EntityFrameworkCore;

namespace GrpcRpcLib.Shared.MessageTools.DataBase;

public class MessageDbContext : DbContext
{
	public DbSet<MessageEnvelope> Messages { get; set; }
	public DbSet<ServiceAddress> ServiceAddresses { get; set; }

	public MessageDbContext(DbContextOptions<MessageDbContext> options) : base(options) { }

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<MessageEnvelope>()
			.HasKey(m => m.Id);

		modelBuilder.Entity<ServiceAddress>()
			.HasKey(s => s.ServiceName);
	}
}