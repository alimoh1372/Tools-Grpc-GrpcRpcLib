using System.Diagnostics;
using System.Text;
using GrpcRpcLib.Publisher.Extensions;
using GrpcRpcLib.Publisher.Services;
using GrpcRpcLib.Shared.Entities.Models;
using GrpcRpcLib.Shared.MessageTools;
using GrpcRpcLib.Shared.MessageTools.DataBase;
using GrpcRpcLib.Shared.MessageTools.Dtos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

// Setup DI
var services = new ServiceCollection();

services.AddLogging(b => b.AddConsole());

var configs = new ConfigurationBuilder()
	.SetBasePath(AppContext.BaseDirectory)
	.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
	.Build();

services.AddMessageStore(configs);

services.AddGrpcPublisher(configs);

// Add your other services (config, store, dbContext, resolver) - فرض بر mock یا real
var provider = services.BuildServiceProvider();

var publisher = provider.GetRequiredService<GrpcPublisher>(); // یا new با inject

var db = provider.GetRequiredService<MessageDbContext>();

await db.ServiceAddresses.AddAsync(new ServiceAddress
{
	ServiceName = "21",
	Address = @"http:\\127.0.0.1:5000"
});

await db.ServiceAddresses.AddAsync(new ServiceAddress
{
	ServiceName = "32",
	Address = @"http:\\127.0.0.1:6000"
});
await db.SaveChangesAsync();

var result =await publisher.Initialize();

if (result.success == false)
{
	Console.WriteLine($"Can't start publisher.ErrorMessage:{result.errorMessage}");
}

Console.WriteLine("Press key to start tests...");
Console.ReadKey();
// Test 1: Single message
var sw = Stopwatch.StartNew();
long startMem = Process.GetCurrentProcess().PrivateMemorySize64;

var envelope = new MessageEnvelope
{
	Type = "test",
	Payload = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new TestMessageType { MessageContent = "test" }))
};

result = await publisher.SendAsync(envelope);

long endMem = Process.GetCurrentProcess().PrivateMemorySize64;
sw.Stop();

Console.WriteLine($"Single message: Time = {sw.ElapsedMilliseconds}ms, Memory delta = {endMem - startMem} bytes, Success = {result.success}");
Console.WriteLine("Press key to start tests 100 message...");
Console.ReadKey();
// Test 100 messages
sw = Stopwatch.StartNew();
startMem = Process.GetCurrentProcess().PrivateMemorySize64;

for (int i = 0; i < 100; i++)
{
	await publisher.SendAsync(envelope);
}

endMem = Process.GetCurrentProcess().PrivateMemorySize64;
sw.Stop();

Console.WriteLine($"100 messages: Time = {sw.ElapsedMilliseconds}ms, Memory delta = {endMem - startMem} bytes");

// Test 1000 messages
sw = Stopwatch.StartNew();
startMem = Process.GetCurrentProcess().PrivateMemorySize64;

for (int i = 0; i < 1000; i++)
{
	await publisher.SendAsync(envelope);
}

endMem = Process.GetCurrentProcess().PrivateMemorySize64;
sw.Stop();

Console.WriteLine($"1000 messages: Time = {sw.ElapsedMilliseconds}ms, Memory delta = {endMem - startMem} bytes");