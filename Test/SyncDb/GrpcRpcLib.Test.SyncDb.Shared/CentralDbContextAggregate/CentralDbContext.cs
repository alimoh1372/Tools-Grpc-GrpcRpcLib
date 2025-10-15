using GrpcRpcLib.Test.SyncDb.Shared.Entities;
using GrpcRpcLib.Test.SyncDb.Shared.EntityConfiguration;
using Microsoft.EntityFrameworkCore;

namespace GrpcRpcLib.Test.SyncDb.Shared.CentralDbContextAggregate;

public class CentralDbContext(DbContextOptions<CentralDbContext> opt) : DbContext(opt)
{
	public DbSet<User> Users => Set<User>();
	public DbSet<Product> Products => Set<Product>();

	public DbSet<Event> Events => Set<Event>();
	public DbSet<AggregateSequence> AggregateSequences => Set<AggregateSequence>();
	public DbSet<ReplayJob> ReplayJobs => Set<ReplayJob>();

	protected override void OnModelCreating(ModelBuilder b)
	{
		b.Entity<User>(e =>
		{
			e.HasKey(x =>x.Id );

			e.Property(x => x.Id).ValueGeneratedNever(); // مهم: خودمان Id می‌دهیم

			e.HasData(
				new User { Id = 1, Username = "u1", FullName = "User One" },
				new User { Id = 2, Username = "u2", FullName = "User Two" },
				new User { Id = 3, Username = "u3", FullName = "User Three" }
			);
		});

		b.Entity<Product>(e =>
		{
			e.HasKey(x => x.Id);

			e.Property(x => x.Id).ValueGeneratedNever();

			e.HasData(
				new Product { Id = 1, Sku = "P-001", Title = "Prod A" },
				new Product { Id = 2, Sku = "P-002", Title = "Prod B" },
				new Product { Id = 3, Sku = "P-003", Title = "Prod C" }
			);
		});

		b.ApplyConfiguration(new EventConfiguration());
		b.ApplyConfiguration(new AggregateSequenceConfiguration());
		b.ApplyConfiguration(new ReplayJobConfiguration());
	}
}