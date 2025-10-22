using System.Text;
using GrpcRpcLib.Consumer.Abstractions;
using GrpcRpcLib.Consumer.Dtos.Attributes;
using GrpcRpcLib.Test.SyncDb.Shared.CentralDbContextAggregate;
using GrpcRpcLib.Test.SyncDb.Shared.Entities;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace GrpcRpcLib.Test.SyncDb.ConsumerA.Processors;

public class CreateUserProcessor:IGrpcProcessor
{
	private readonly IServiceProvider _serviceProvider;

	public CreateUserProcessor(IServiceProvider serviceProvider)
	{
		_serviceProvider = serviceProvider;
	}

	[GrpcProcessor("AddUser")]
	public void  CreateUserProcess(Event addUserEvent)
	{
		var user = JsonConvert.DeserializeObject<User>(Encoding.UTF8.GetString(addUserEvent.Payload))!;

		using var scope = _serviceProvider.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<CentralDbContext>();
		db.Users.Add(user);
		db.SaveChanges();

		//Add Event to Proce
	}
}