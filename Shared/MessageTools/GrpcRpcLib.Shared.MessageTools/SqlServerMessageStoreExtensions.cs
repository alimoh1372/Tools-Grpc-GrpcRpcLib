using GrpcRpcLib.Shared.MessageTools.Abstraction;
using GrpcRpcLib.Shared.MessageTools.DataBase;
using GrpcRpcLib.Shared.MessageTools.Implementation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GrpcRpcLib.Shared.MessageTools;

public static class SqlServerMessageStoreExtensions
{
	public static IServiceCollection AddSqlServerMessageStore(
		this IServiceCollection services, 
		string connectionString, 
		string prefix = "",
		bool autoMigrate = true)
	{
		services.AddDbContext<MessageStoreDbContext>(options =>
			options.UseSqlServer(connectionString));

		services.AddScoped<IMessageStore>(sp =>
		{
			var dbContext = sp.GetRequiredService<MessageStoreDbContext>();

			if (autoMigrate)
			{
				dbContext.Database.Migrate();
			}
			return new SqlServerMessageStore(dbContext);
		});

		// Optional: Seed or ensure ServiceAddresses table is ready, but since no methods in IMessageStore use it, assuming it's for future use.

		return services;
	}
}