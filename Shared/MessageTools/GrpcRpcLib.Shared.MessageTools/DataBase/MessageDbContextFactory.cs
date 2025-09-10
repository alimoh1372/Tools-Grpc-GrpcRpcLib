using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using GrpcRpcLib.Shared.MessageTools.Dtos.Configurations;

namespace GrpcRpcLib.Shared.MessageTools.DataBase;

public class MessageDbContextFactory : IDesignTimeDbContextFactory<MessageDbContext>
{
	public MessageDbContext CreateDbContext(string[] args)
	{
		var configuration = new ConfigurationBuilder()
			.SetBasePath(AppContext.BaseDirectory)
			.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
			.Build();

		var storeType = configuration.GetValue<string>($"{MessageStoreConfiguration.SectionName}:Type", "Sqlite");
		var connectionString = configuration.GetValue<string>($"{MessageStoreConfiguration.SectionName}:ConnectionString", "Data Source=consumer.db");
		var prefix = configuration.GetValue<string>($"{MessageStoreConfiguration.SectionName}:Prefix", "");

		var optionsBuilder = new DbContextOptionsBuilder<MessageDbContext>();

		switch (storeType)
		{
			case "SqlServer":
				optionsBuilder.UseSqlServer(connectionString);
				break;
			case "Sqlite":
				optionsBuilder.UseSqlite(connectionString);
				break;
			case "InMemory":
				optionsBuilder.UseInMemoryDatabase("GrpcTestDb");
				break;
			default:
				throw new InvalidOperationException($"Unsupported store type: {storeType}");
		}

		return new MessageDbContext(optionsBuilder.Options, prefix);
	}
}