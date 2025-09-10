using GrpcRpcLib.Consumer.Abstractions;
using GrpcRpcLib.Shared.MessageTools.Abstraction;
using Microsoft.Extensions.DependencyInjection;

namespace GrpcRpcLib.Consumer.Extensions;

public class GrpcConsumerServerOptions(IServiceCollection services)
{
	public GrpcConsumerServerOptions AddStoreMessage<TStore>(
		Action<TStore>? configureStore = null) where TStore : class, IMessageStore
	{
		//Can config message store here
		return this;
	}

	public GrpcConsumerServerOptions AddProcessor<TProcessor>() where TProcessor : class, IGrpcProcessor
	{
		//services.AddTransient<TProcessor>();
		return this;
	}
}