using GrpcRpcLib.Shared.MessageTools.Dtos;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace GrpcRpcLib.Shared.MessageTools.DataBase.Models;

public class ServiceAddressConfiguration(string prefix = "") : IEntityTypeConfiguration<ServiceAddress>
{
	public void Configure(EntityTypeBuilder<ServiceAddress> builder)
	{
		builder.ToTable(prefix + "ServiceAddresses");
		builder.HasKey(s => s.ServiceName);
		builder.Property(s => s.ServiceName).IsRequired().HasMaxLength(256);
		builder.Property(s => s.Address).IsRequired().HasMaxLength(512);
	}
}