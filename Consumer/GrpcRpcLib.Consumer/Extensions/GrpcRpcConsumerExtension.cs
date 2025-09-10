using GrpcRpcLib.Consumer.Abstractions;
using GrpcRpcLib.Consumer.Dtos.Configurations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Builder;
using GrpcRpcLib.Consumer.Implementations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace GrpcRpcLib.Consumer.Extensions;

public static class GrpcConsumerServerExtensions
{
	public static IHostBuilder UseGrpcConsumerServer(
		this IHostBuilder hostBuilder, 
		Action<GrpcConsumerServerOptions>? configureOptions = null
		)
	{
		GrpcRpcConsumerConfiguration? grpcConfiguration = null;

		return hostBuilder.ConfigureServices((context, services) =>
		{
			var config = new ConfigurationBuilder()
				.SetBasePath(AppContext.BaseDirectory)
				.AddConfiguration(context.Configuration)
				.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
				.Build()
				;

			var grpcConfiguration= config
				.GetSection(GrpcRpcConsumerConfiguration.SectionName)
				.Get<GrpcRpcConsumerConfiguration>();

			if (grpcConfiguration==null )
				throw new Exception("The GrpcRpcConsumerConfiguration section not found or config is wrong.");


			// تنظیمات Consumer از appsettings.json
			services.Configure<GrpcRpcConsumerConfiguration>(
				context.Configuration!.GetSection(GrpcRpcConsumerConfiguration.SectionName)
			);

			// اضافه کردن سرویس‌های Consumer به DI با خواندن تنظیمات از configuration
			services.AddConsumerServicesToDi(context.Configuration);


			services.Configure<KestrelServerOptions>(options =>
			{
				options.ListenAnyIP(grpcConfiguration.Port, listenOptions =>
				{
					listenOptions.Protocols = HttpProtocols.Http2; // صراحتاً HTTP/2 برای gRPC
				});
			});
			// اعمال تنظیمات custom از configureOptions
			var options = new GrpcConsumerServerOptions(services);

			configureOptions?.Invoke(options);

		}).ConfigureWebHostDefaults(builder =>
		{
			// خواندن تنظیمات برای آدرس هاست
			
			if ( grpcConfiguration !=null && !string.IsNullOrEmpty(grpcConfiguration.Host) && grpcConfiguration.Port>0)
			{
				builder.UseUrls($"http\\{grpcConfiguration.Host}:{grpcConfiguration.Port}"); // تنظیم آدرس از appsettings.json
			}
			else
			{
				builder.UseUrls("http://localhost:5000"); // fallback
				

			}

			builder.Configure(app =>
			{
				app.UseRouting();

				app.UseGrpcServer(); // تنظیمات اضافی gRPC server

				app.UseEndpoints(endpoints =>
				{
					endpoints.MapGrpcService<GrpcConsumerService>();
				});

			});
		});
	}

	public static IServiceCollection AddGrpcConsumerServer(this IServiceCollection services, Action<GrpcConsumerServerOptions>? configureOptions = null)
	{
		// اضافه کردن سرویس‌های Consumer به DI
		services.AddConsumerServicesToDi(null); // Configuration در UseGrpcConsumerServer پاس می‌شود

		// اعمال تنظیمات custom
		var options = new GrpcConsumerServerOptions(services);

		configureOptions?.Invoke(options);

		return services;
	}

	private static IServiceCollection AddConsumerServicesToDi(
		this IServiceCollection services,
		IConfiguration? configuration
		)

	{

		// اضافه کردن سرویس‌های gRPC و Consumer
		services.AddGrpc();

		services.AddSingleton<GrpcConsumerService>();


		// اضافه کردن processorها (مثل TestTypeProcessor)
		services.AddTransient<IGrpcProcessor,TestTypeProcessor>();

		return services;
	}

	private static IApplicationBuilder UseGrpcServer(this IApplicationBuilder app)
	{
		// تنظیمات اضافی اگر نیاز (مثلاً middleware برای logging)

		return app;
	}
}

