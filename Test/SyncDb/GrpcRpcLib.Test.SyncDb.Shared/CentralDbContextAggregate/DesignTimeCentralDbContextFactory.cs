using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace GrpcRpcLib.Test.SyncDb.Shared.CentralDbContextAggregate;

// this class will be used by dotnet-ef at design time to create CentralDbContext
public class DesignTimeCentralDbContextFactory : IDesignTimeDbContextFactory<CentralDbContext>
{
	public CentralDbContext CreateDbContext(string[] args)
	{
		// try to read connection string from environment variable first (safer for CI)
		var conn = Environment.GetEnvironmentVariable("CENTRAL_CONNECTION_STRING");

		if (string.IsNullOrWhiteSpace(conn))
		{
			// fallback: read appsettings.json in project directory
			var basePath = Directory.GetCurrentDirectory();

			var cfg = new ConfigurationBuilder()
				.SetBasePath(basePath)
				.AddJsonFile("appsettings.json", optional: true)
				.AddEnvironmentVariables()
				.Build();

			conn = cfg.GetConnectionString("SqlCentralDb");

			if (string.IsNullOrWhiteSpace(conn))
				throw new InvalidOperationException("Connection string for CentralDbContext not found. Set CENTRAL_CONNECTION_STRING env var or ConnectionStrings:Sql in appsettings.json.");
		}

		var optionsBuilder = new DbContextOptionsBuilder<CentralDbContext>();
		
		optionsBuilder.UseSqlServer(conn); // adjust if you use Sqlite or other provider

		return new CentralDbContext(optionsBuilder.Options);
	}
}