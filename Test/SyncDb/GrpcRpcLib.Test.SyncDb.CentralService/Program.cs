using GrpcRpcLib.Publisher.Extensions;
using GrpcRpcLib.Shared.MessageTools;
using GrpcRpcLib.Test.SyncDb.CentralService;
using GrpcRpcLib.Test.SyncDb.Shared.Abstractions;
using GrpcRpcLib.Test.SyncDb.Shared.CentralDbContextAggregate;
using GrpcRpcLib.Test.SyncDb.Shared.Implementations;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using SerilogLogger.Implementation;


var builder = WebApplication.CreateBuilder(args);

var configs = new ConfigurationBuilder()
	.SetBasePath(AppContext.BaseDirectory)
	.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
	.Build();

builder.Services.AddDbContext<CentralDbContext>(opt =>
	opt.UseSqlServer(builder.Configuration.GetConnectionString("SqlCentralDb")));

builder.Services.AddMessageStore(configs);

builder.Services.AddGrpcPublisher(configs);

builder.Services.AddLoggerDependencies(configs);

builder.Services.AddControllers();

builder.Services.AddSingleton<ISequenceProvider, SqlServerSequenceProvider>();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddHostedService<MainWorker>();

builder.Services.AddSwaggerGen(c =>
{
	c.SwaggerDoc("v1", new OpenApiInfo { Title = "Central API", Version = "v1" });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
	var db = scope.ServiceProvider.GetRequiredService<CentralDbContext>();
	db.Database.Migrate();
}

// Enable swagger and map it to root
app.UseSwagger();

// Put swagger UI at the root so http://localhost:5117/ opens swagger
app.UseSwaggerUI(c =>
{
	c.SwaggerEndpoint("/swagger/v1/swagger.json", "Central API v1");
	c.RoutePrefix = ""; // make swagger available at root
});

// If you also want a simple text response at /health or /, you can map a GET
app.MapGet("/health", () => Results.Ok("Central API is up"));

app.MapControllers();



app.Run();
