using GrpcRpcLib.Publisher.Configurations;
using GrpcRpcLib.Publisher.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GrpcRpcLib.Publisher.Extensions;

public static class GrpcPublisherExtensions
{
	public static IServiceCollection AddGrpcPublisher(this IServiceCollection services, IConfiguration configuration)
	{
		// Bind GrpcPublisherConfiguration from appsettings.json section
		services.Configure<GrpcPublisherConfiguration>(configuration.GetSection(GrpcPublisherConfiguration.SectionName));

		// Add DbContext for fetching addresses (assuming MessageDbContext is already configured elsewhere, e.g., in MessageToolsDependencyInjection)
		// If not, add it here or assume it's added separately

		// Add scoped service to resolve and cache addresses
		services.AddScoped<AddressResolver>();

		// Optionally add IMemoryCache if not already added
		services.AddMemoryCache();

		return services;
	}
}