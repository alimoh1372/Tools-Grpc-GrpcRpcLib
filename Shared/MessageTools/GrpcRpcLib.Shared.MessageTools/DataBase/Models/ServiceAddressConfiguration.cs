using GrpcRpcLib.Shared.MessageTools.Dtos;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace GrpcRpcLib.Shared.MessageTools.DataBase.Models;

public class ServiceAddressConfiguration() : IEntityTypeConfiguration<ServiceAddress>
{
	public void Configure(EntityTypeBuilder<ServiceAddress> builder)
	{
		builder.ToTable("ServiceAddresses");

		builder.HasKey(x => x.Id);

		builder.Property(s => s.CurrentService)
			.IsRequired();

		builder.Property(s => s.ServiceName).IsRequired().HasMaxLength(256);
		
		builder.Property(s => s.Address).IsRequired().HasMaxLength(512);
	}
}