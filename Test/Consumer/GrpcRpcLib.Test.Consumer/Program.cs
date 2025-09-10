using Microsoft.Extensions.Hosting;
using GrpcRpcLib.Consumer.Extensions;
using GrpcRpcLib.Consumer.Implementations;
using GrpcRpcLib.Shared.MessageTools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;


var hostBuilder = Host.CreateDefaultBuilder(args)
	.ConfigureServices(services =>
	{
		var config=new ConfigurationBuilder()
		.SetBasePath(AppContext.BaseDirectory)
		.AddJsonFile("appsettings.json",optional:false,reloadOnChange:true)
		.Build();

		services.AddMessageStore(config);

	})
	.UseGrpcConsumerServer()
	.UseWindowsService();

var host=hostBuilder.Build();

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
