using GrpcRpcLib.Shared.Entities.Models;
using GrpcRpcLib.Shared.MessageTools.DataBase.Models;
using GrpcRpcLib.Shared.MessageTools.Dtos;
using Microsoft.EntityFrameworkCore;

namespace GrpcRpcLib.Shared.MessageTools.DataBase;

public class MessageDbContext(DbContextOptions<MessageDbContext> options, string prefix = "")
	: DbContext(options)
{
	public DbSet<MessageEnvelope> Messages { get; set; }
	public DbSet<ServiceAddress> ServiceAddresses { get; set; }

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<MessageEnvelope>()
			.ToTable($"{prefix}_Messages")
			.HasKey(m => m.Id);

		modelBuilder.Entity<ServiceAddress>()
			.ToTable($"{prefix}_ServiceAddresses")
			.HasKey(s => s.ServiceName);
	}
}