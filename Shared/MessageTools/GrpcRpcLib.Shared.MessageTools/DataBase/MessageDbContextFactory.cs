using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using GrpcRpcLib.Shared.MessageTools.Dtos.Configurations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GrpcRpcLib.Shared.MessageTools.DataBase;

public class MessageDbContextFactory : IDesignTimeDbContextFactory<MessageDbContext>
{
	
	public MessageDbContext CreateDbContext(string[] args)
	{
		var basePath = AppContext.BaseDirectory;
		var configuration = new ConfigurationBuilder()
			.SetBasePath(basePath)
			.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
			.AddEnvironmentVariables() // allow override by env var
			.Build();

		MessageStoreConfiguration config = configuration.GetSection(MessageStoreConfiguration.SectionName)
			.Get<MessageStoreConfiguration>() ?? new MessageStoreConfiguration();
		
		//var storeType = configuration.GetValue<string>($"{MessageStoreConfiguration.SectionName}:StorageType", "Sqlite");
		//var connectionString = configuration.GetValue<string>($"{MessageStoreConfiguration.SectionName}:StorageConnectionString", "Data Source=consumer.db");
		//var prefix = configuration.GetValue<string>($"{MessageStoreConfiguration.SectionName}:StoragePrefix", "");

		var optionsBuilder = new DbContextOptionsBuilder<MessageDbContext>();

		switch (config.StorageType)
		{
			case "SqlServer":
				optionsBuilder.UseSqlServer(config.StorageConnectionString);
				break;
			case "Sqlite":
				optionsBuilder.UseSqlite(config.StorageConnectionString);
				break;
			case "InMemory":
				optionsBuilder.UseInMemoryDatabase("GrpcTestDb");
				break;
			default:
				throw new InvalidOperationException($"Unsupported store type: {config.StorageType}");
		}

		return new MessageDbContext(optionsBuilder.Options);
	}
}