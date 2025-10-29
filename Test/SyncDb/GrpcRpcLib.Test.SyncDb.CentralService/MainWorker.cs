using GrpcRpcLib.Publisher.Services;
using GrpcRpcLib.Test.SyncDb.Shared.CentralDbContextAggregate;
using Microsoft.EntityFrameworkCore;
using System.Data;
using Dapper;
using GrpcRpcLib.Publisher.Configurations;
using GrpcRpcLib.Test.SyncDb.Shared.Entities;

namespace GrpcRpcLib.Test.SyncDb.CentralService;

public class MainWorker : BackgroundService
{
	private readonly IServiceProvider _serviceProvider;

	private readonly GrpcPublisher _publisher;

	private readonly TimeSpan _pollDelay = TimeSpan.FromSeconds(2);

	private readonly TimeSpan _watchdogPeriod = TimeSpan.FromMinutes(1);

	private readonly TimeSpan _processingTimeout = TimeSpan.FromHours(24);

	private CancellationToken _stoppingToken;
	private const string ProcessorInstanceId="ApplicationId_1";
	private const string sqlClaimQuery = @"
WITH cte AS (
    SELECT TOP (1) *
    FROM dbo.Events
    WHERE Status = @statusPending And ProcessorInstace
    ORDER BY Priority DESC, SequenceNumber ASC, CreatedAt ASC
)
UPDATE cte
SET 
    Status = @statusProcessing,
    ProcessorInstanceId = @procId,
    Attempts = ISNULL(Attempts,0) + 1,
    LastAttemptAt = SYSUTCDATETIME()
OUTPUT INSERTED.*;
";

	public MainWorker(GrpcPublisher publisher, IConfiguration config, IServiceProvider serviceProvider)
	{
		_publisher = publisher;

		var publisherConfig = config.GetSection(GrpcPublisherConfiguration.SectionName)
			.Get<GrpcPublisherConfiguration>();

		_publisher.SetConfigs(publisherConfig ?? new GrpcPublisherConfiguration());

		_serviceProvider = serviceProvider;
	}

	public override async Task StartAsync(CancellationToken cancellationToken)
	{ 
		_stoppingToken = cancellationToken;

		var result=await _publisher.Initialize(_stoppingToken);

		//if (!result.success)
		//{
		//	//LogError
		//	return;
		//}
			

		await base.StartAsync(cancellationToken);	
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		_stoppingToken = stoppingToken;

		var tasks = new List<Task>();

		try
		{
			tasks.Add(Task.Run(async () => await WatchdogLoop(stoppingToken), stoppingToken));

			tasks.Add(Task.Run(async () => await Processing(_stoppingToken), _stoppingToken));

			await Task.WhenAll(tasks);

		}
		catch (Exception ex)
		{

		}
	}

	private async Task Processing(CancellationToken stoppingToken)
	{
		while (!stoppingToken.IsCancellationRequested)
		{
			Guid? eventId = null;
			try
			{
				await using var scope = _serviceProvider.CreateAsyncScope();

				var db = scope.ServiceProvider.GetRequiredService<CentralDbContext>();

				var ev = await ClaimOnePendingEventAsync(db, stoppingToken);

				if (ev == null)
				{
					await Task.Delay(_pollDelay, stoppingToken);

					continue;
				}

				eventId = ev.EventId;

				var envelope = new GrpcRpcLib.Shared.Entities.Models.MessageEnvelope
				{
					Type = ev.EventType,
					CorrelationId = ev.EventId.ToString(),
					Priority = ev.Priority,
					Payload = ev.Payload,
					CreatedAt = ev.CreatedAt,
					Status = "TryingToPublish"
				};

				var result = await _publisher.SendAsync(envelope);

				//var messagesDb = scope.ServiceProvider.GetRequiredService<MessageDbContext>();



				////Get other service address sent to all 
				//foreach (var serviceAddress in await messagesDb.ServiceAddresses.Where(x=>x.CurrentService==false).ToListAsync(_stoppingToken))
				//{
				//	var (ok, err) = await _publisher.SendAsync(envelope);
				//}

				if (result.success)
					await MarkEventAfterPublishAsync(db, ev.EventId, true, null, stoppingToken);
				else
					await MarkEventAfterPublishAsync(db, ev.EventId, false, result.errorMessage, stoppingToken);

			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				if (eventId != null)
				{
					await using var scope = _serviceProvider.CreateAsyncScope();

					var db = scope.ServiceProvider.GetRequiredService<CentralDbContext>();

					await MarkEventAfterPublishAsync(db, eventId.Value, false, null, stoppingToken);
				}
				

			}
			catch (Exception ex)
			{
				if (eventId != null)
				{
					await using var scope = _serviceProvider.CreateAsyncScope();

					var db = scope.ServiceProvider.GetRequiredService<CentralDbContext>();

					await MarkEventAfterPublishAsync(db, eventId.Value, false, null, stoppingToken);
				}

				await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
			}
		}
	}

	

	private async Task MarkEventAfterPublishAsync(CentralDbContext db, Guid eventId, bool success,
		string? errorMessage, CancellationToken ct)
	{
		
		var conn = db.Database.GetDbConnection();

		if (conn.State != ConnectionState.Open) await conn.OpenAsync(ct);

		if (success)
		{
			var sql = @"UPDATE dbo.Events
                    SET Status = @status, LastAttemptAt = SYSUTCDATETIME(), ProcessorInstanceId = @instanceId, ErrorMessage = NULL
                    WHERE EventId = @id";

			await conn.ExecuteAsync(sql, new { status = "PublishedToOtherServiceCompletely",instanceId=ProcessorInstanceId, id = eventId });
		}

		else
		{
			var sql = $"""
			           UPDATE dbo.Events
			                               SET Status = @status, LastAttemptAt = SYSUTCDATETIME(), ProcessorInstanceId = @instanceId, ErrorMessage = @err
			                               WHERE EventId = @id
			           """;

			await conn.ExecuteAsync(sql, new { status = "Failed", id = eventId, instanceId=ProcessorInstanceId, err = errorMessage ?? "" });

		}
	}
	private async Task<Event?> ClaimOnePendingEventAsync(CentralDbContext db, CancellationToken ct)
	{
		var conn = db.Database.GetDbConnection();

		if (conn.State != ConnectionState.Open) await conn.OpenAsync(ct);

		var parameters = new
		{
			statusPending = "Pending",
			statusProcessing = "Processing",
			procId =ProcessorInstanceId  // Guid worker instance id field
		};

		// Dapper: QuerySingleOrDefault returns a dynamic / map to a POCO
		var row = await conn.QuerySingleOrDefaultAsync<Event>(sqlClaimQuery, parameters);

		if (row == null) return null;

		return row;
	}

	private async Task WatchdogLoop(CancellationToken stoppingToken)
	{
		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await RequeueStuckProcessingAsync(stoppingToken);
			}
			catch (Exception ex)
			{

			}

			await Task.Delay(_watchdogPeriod, stoppingToken);
		}
	}

	private async Task RequeueStuckProcessingAsync(CancellationToken ct)
	{
		await using var scope = _serviceProvider.CreateAsyncScope();

		var db = scope.ServiceProvider.GetRequiredService<CentralDbContext>();

		var conn = db.Database.GetDbConnection();

		if (conn.State != ConnectionState.Open) await conn.OpenAsync(ct);

		var cutoff = DateTime.UtcNow - _processingTimeout;

		var sql = @"UPDATE dbo.Events
                    SET Status = @pending, ProcessorInstanceId = @processorInstance, LastAttemptAt = NULL
                    WHERE Status = @processing AND LastAttemptAt < @cutoff";

		await conn.ExecuteAsync(sql, new { pending = "Pending", processing = "Processing",processorInstance=ProcessorInstanceId, cutoff });
	}
}