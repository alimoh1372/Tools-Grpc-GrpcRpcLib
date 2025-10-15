using GrpcRpcLib.Shared.MessageTools.Abstraction;
using GrpcRpcLib.Shared.MessageTools.DataBase;
using GrpcRpcLib.Shared.MessageTools.Dtos.Configurations;
using GrpcRpcLib.Shared.MessageTools.Implementation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GrpcRpcLib.Shared.MessageTools;
public static class MessageToolsDependencyInjection
{
	/// <summary>
	/// <example>
	/// "MessageStore": {
	///"Type": "SqlServer", // or "Sqlite"
	///"ConnectionString": "Server=...;Database=...;",
	///"Prefix": "MyApp_" // e.g., tables will be "MyApp_MessageEnvelopes"
	///}
	/// use this in <code>appsetting.json</code> or other setting json file
	/// </example>
	/// </summary>
	/// <param name="services"></param>
	/// <param name="configuration"></param>
	/// <returns></returns>
	public static IServiceCollection AddMessageStore(this IServiceCollection services, IConfiguration configuration)
	{
		services.Configure<MessageStoreConfiguration>(configuration.GetSection(MessageStoreConfiguration.SectionName));

		var storageType = configuration.GetValue<string>($"{MessageStoreConfiguration.SectionName}:StorageType", "InMemory");

		var connectionString = configuration.GetValue<string>($"{MessageStoreConfiguration.SectionName}:StorageConnectionString", "Data Source=messages.db");

		var prefix = configuration.GetValue<string>($"{MessageStoreConfiguration.SectionName}:StoragePrefix", "");
		
		switch (storageType)
		{
			case "SqlServer":
				services.AddDbContext<MessageDbContext>(
					option =>
				{
					if (!option.IsConfigured)
					{
						option.UseSqlServer(connectionString);
					}
				});

				services.AddSingleton<IMessageStore, SqlServerMessageStore>();

				var sp=services.BuildServiceProvider();
				
				var db=sp.GetRequiredService<MessageDbContext>();
				
				db.Database.Migrate();

				break;

			case "Sqlite":
				services.AddDbContext<MessageDbContext>(options =>
				{
					options.UseSqlite(connectionString);
				});

				services.AddSingleton<IMessageStore,SqliteMessageStore>();
				
				var serviceProvider=services.BuildServiceProvider();

				using (var scope = serviceProvider.CreateScope())
				{
					var sqliteMessageDbContext = scope.ServiceProvider.GetRequiredService<MessageDbContext>();

					sqliteMessageDbContext.Database.EnsureCreated();
				}

				break;

			case "InMemory":
				services.AddDbContext<MessageDbContext>(options =>
				{
					options.UseInMemoryDatabase("GrpcTestDb"); // نام دیتابیس ثابت برای تست
				}, ServiceLifetime.Scoped);

				services.AddScoped<IMessageStore>(sp => new InMemoryMessageStore());

				break;

			default:
				throw new InvalidOperationException($"Unsupported store type: {storageType}");

				break;
		}

		return services;
	}
}