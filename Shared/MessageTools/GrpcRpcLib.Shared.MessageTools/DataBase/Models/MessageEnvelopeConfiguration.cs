using GrpcRpcLib.Shared.Entities.Models;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace GrpcRpcLib.Shared.MessageTools.DataBase.Models;

public class MessageEnvelopeConfiguration() : IEntityTypeConfiguration<MessageEnvelope>
{
	public void Configure(EntityTypeBuilder<MessageEnvelope> builder)
	{
		builder.ToTable("MessageEnvelopes");
		builder.HasKey(m => m.Id);
		builder.Property(m => m.TargetId)
			.IsRequired()
			.HasMaxLength(256);
		builder.Property(m => m.Type).IsRequired().HasMaxLength(256);
		builder.Property(m => m.CorrelationId).HasMaxLength(256);
		builder.Property(m => m.ReplyToId).HasMaxLength(512);
		builder.Property(m => m.Payload).IsRequired();
		builder.Property(m => m.CreatedAt).IsRequired();
		builder.Property(m => m.Status).IsRequired().HasMaxLength(50);
		builder.Property(m => m.RetryCount).IsRequired();
		builder.Property(m => m.LastRetryAt);
		builder.Property(m => m.ErrorMessage).HasMaxLength(2048);
	}
}