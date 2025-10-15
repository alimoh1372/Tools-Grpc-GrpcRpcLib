using GrpcRpcLib.Test.SyncDb.Shared.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace GrpcRpcLib.Test.SyncDb.Shared.EntityConfiguration;

public class AggregateSequenceConfiguration : IEntityTypeConfiguration<AggregateSequence>
{
	public void Configure(EntityTypeBuilder<AggregateSequence> builder)
	{
		builder.ToTable("AggregateSequences");

		builder.HasKey(x => new { x.AggregateType, x.AggregateId });

		builder.Property(x => x.AggregateType).HasMaxLength(100).IsRequired();

		builder.Property(x => x.AggregateId).IsRequired();

		builder.Property(x => x.LastSequence).IsRequired().HasDefaultValue(0);
	}
}