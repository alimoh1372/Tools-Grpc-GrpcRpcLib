using GrpcRpcLib.Shared.MessageTools.Abstraction;
using GrpcRpcLib.Shared.MessageTools.DataBase;
using GrpcRpcLib.Shared.MessageTools.Implementation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using GrpcRpcLib.Shared.MessageTools.Abstraction;
using GrpcRpcLib.Shared.MessageTools.DataBase;

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
		var storageType = configuration.GetValue<string>("MessageStore:Type", "InMemory");
		var connectionString = configuration.GetValue<string>("MessageStore:ConnectionString", "Data Source=messages.db");
		var prefix = configuration.GetValue<string>("MessageStore:Prefix", "");

		switch (storageType)
		{
			case "SqlServer":
				services.AddDbContext<MessageDbContext>(options =>
				{
					options.UseSqlServer(connectionString);
					// Apply prefix to DbContext via options if needed, but since prefix is used in OnModelCreating, pass it via factory or options
					// For simplicity, we'll use a factory to inject prefix
				}, ServiceLifetime.Scoped);

				services.AddScoped<IMessageStore>(sp =>
				{
					var dbContext = sp.GetRequiredService<MessageDbContext>();
					// Ensure migrations are applied
					dbContext.Database.Migrate();
					return new SqlServerMessageStore(dbContext, prefix);
				});

				// Register prefix for DbContext if needed, but since DbContext constructor takes prefix, use factory
				services.AddScoped(provider => new MessageDbContext(
					provider.GetRequiredService<DbContextOptions<MessageDbContext>>(),
					prefix
				));
				break;

			case "Sqlite":
				services.AddDbContext<MessageDbContext>(options =>
				{
					options.UseSqlite(connectionString);
				}, ServiceLifetime.Scoped);

				services.AddScoped<IMessageStore>(sp =>
				{
					var dbContext = sp.GetRequiredService<MessageDbContext>();
					// For SQLite, use EnsureCreated or Migrate
					dbContext.Database.EnsureCreated(); // or Migrate() if using migrations
					return new SqliteMessageStore(dbContext, prefix);
				});

				// Register prefix for DbContext
				services.AddScoped(provider => new MessageDbContext(
					provider.GetRequiredService<DbContextOptions<MessageDbContext>>(),
					prefix
				));
				break;

			default:
				// Fallback to InMemory or other implementation if needed
				services.AddScoped<IMessageStore>(sp => new InMemoryMessageStore()); // Assuming InMemoryMessageStore exists
				break;
		}

		return services;
	}
}