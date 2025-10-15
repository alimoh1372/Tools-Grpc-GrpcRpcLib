using GrpcRpcLib.Test.SyncDb.Shared.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace GrpcRpcLib.Test.SyncDb.Shared.EntityConfiguration;

public class ReplayJobConfiguration : IEntityTypeConfiguration<ReplayJob>
{
	public void Configure(EntityTypeBuilder<ReplayJob> builder)
	{
		builder.ToTable("ReplayJobs");
		builder.HasKey(x => x.JobId);

		builder.Property(x => x.Status).HasMaxLength(50).HasDefaultValue("Pending");
		builder.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
		builder.HasIndex(x => x.Status);
	}
}