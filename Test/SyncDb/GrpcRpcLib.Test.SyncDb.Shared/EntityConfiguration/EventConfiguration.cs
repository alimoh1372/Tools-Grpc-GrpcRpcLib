using GrpcRpcLib.Test.SyncDb.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrpcRpcLib.Test.SyncDb.Shared.EntityConfiguration;

public class EventConfiguration : IEntityTypeConfiguration<Event>
{
	public void Configure(EntityTypeBuilder<Event> builder)
	{
		builder.ToTable("Events");

		builder.HasKey(x => x.EventId);

		builder.Property(x => x.AggregateType).HasMaxLength(100).IsRequired();

		builder.Property(x => x.EventType).HasMaxLength(100).IsRequired();

		builder.Property(x => x.SequenceNumber).IsRequired();

		builder.Property(x => x.Status).HasMaxLength(50).HasDefaultValue("Pending");

		builder.Property(x => x.Attempts).HasDefaultValue(0);

		builder.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

		builder.HasIndex(x => new { x.AggregateType, x.AggregateId, x.SequenceNumber });

		builder.HasIndex(x => x.Status);
	}
}