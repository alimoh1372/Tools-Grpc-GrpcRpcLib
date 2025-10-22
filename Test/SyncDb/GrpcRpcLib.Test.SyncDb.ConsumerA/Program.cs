using GrpcRpcLib.Consumer.Abstractions;
using GrpcRpcLib.Consumer.Extensions;
using GrpcRpcLib.Consumer.Implementations;
using GrpcRpcLib.Shared.MessageTools;
using GrpcRpcLib.Test.SyncDb.ConsumerA.Processors;
using GrpcRpcLib.Test.SyncDb.Shared.CentralDbContextAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var hostBuilder = Host.CreateDefaultBuilder(args)
	.ConfigureServices(services =>
	{
		var config = new ConfigurationBuilder()
			.SetBasePath(AppContext.BaseDirectory)
			.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
			.Build();

		services.AddTransient<IGrpcProcessor, CreateUserProcessor>();

		//services.AddMessageStore(config);

		services.AddDbContext<CentralDbContext>(opt =>
			opt.UseSqlServer(config.GetConnectionString("SqlCentralDb")));


	})
	.UseGrpcConsumerServer()
	.UseWindowsService();

var host = hostBuilder.Build();

var grpcService = host.Services.GetRequiredService<GrpcConsumerService>();

var result = await grpcService.Initialize();

if (result.success)
{
	await host.RunAsync();
}
else
{
	throw new Exception("Can't initialize grpc consumer service");
}
