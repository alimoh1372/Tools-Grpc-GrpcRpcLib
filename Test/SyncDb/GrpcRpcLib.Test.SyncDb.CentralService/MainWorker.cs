using GrpcRpcLib.Publisher.Services;
using GrpcRpcLib.Test.SyncDb.Shared.CentralDbContextAggregate;
using Microsoft.EntityFrameworkCore;
using System.Data;
using Dapper;
using GrpcRpcLib.Test.SyncDb.Shared.Entities;

namespace GrpcRpcLib.Test.SyncDb.CentralService;

public class MainWorker:BackgroundService
{
	private readonly IServiceProvider _serviceProvider;
	private readonly GrpcPublisher _publisher;
	private readonly string _instanceId; 
	private readonly TimeSpan _pollDelay = TimeSpan.FromSeconds(2);
	private readonly TimeSpan _watchdogPeriod = TimeSpan.FromMinutes(1);
	private readonly TimeSpan _processingTimeout = TimeSpan.FromHours(24);
	private CancellationToken _stoppingToken;

	private const string sqlClaimQuery= @"
WITH cte AS (
    SELECT TOP (1) *
    FROM dbo.Events WITH (ROWLOCK, READPAST, UPDLOCK)
    WHERE Status = @statusPending
    ORDER BY Priority DESC, SequenceNumber ASC, CreatedAt ASC
)
UPDATE cte
SET 
    Status = @statusProcessing,
    ProcessorInstanceId = @procId,
    Attempts = ISNULL(Attempts,0) + 1,
    LastAttemptAt = SYSUTCDATETIME()
OUTPUT INSERTED;
";

	public MainWorker(GrpcPublisher publisher,IConfiguration config, IServiceProvider serviceProvider)
	{
		_publisher = publisher;
		_serviceProvider = serviceProvider;
		_instanceId = config["Properties:ApplicationId"] ?? "UnkonwPublisherConsumer";

	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		_stoppingToken=stoppingToken;
		
		var tasks=new List<Task>();

		try
		{
			tasks.Add(Task.Run(async () =>await WatchdogLoop(stoppingToken), stoppingToken));

			tasks.Add(Task.Run(async()=>await Processing(_stoppingToken),_stoppingToken));

			while (!_stoppingToken.IsCancellationRequested)
			{
				try
				{
					var eventP=await _db.Events(ev=>).FirstOrDefaultAsync()
				}
				catch (Exception ex)
				{
					Console.WriteLine(ex);
					throw;
				}

				await Task.Delay(5000, _stoppingToken);
			}
		}
		catch (Exception ex)
		{
			
		}
	}

	private async Task Processing(CancellationToken stoppingToken)
	{
		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await using var scope = _serviceProvider.CreateAsyncScope();

				var db = scope.ServiceProvider.GetRequiredService<CentralDbContext>();

				var ev = await ClaimOnePendingEventAsync(db,stoppingToken);

				if (ev == null)
				{
					await Task.Delay(_pollDelay, stoppingToken);
					continue;
				}

				var envelope = new GrpcRpcLib.Shared.Entities.Models.MessageEnvelope
				{
					Id = ev.EventId,
					Type = ev.EventType,
					CorrelationId = ev.EventId.ToString(),
					Priority = ev.Priority,
					Payload = ev.Payload,
					CreatedAt = ev.CreatedAt,
					Status = "TryingToPublish"
				};
				//Get other service address sent to all 
				var (ok, err) = await _publisher.SendAsync(envelope);

				await MarkEventAfterPublishAsync(ev.EventId, ok, err, stoppingToken);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
			catch (Exception ex)
			{
				_logger.LogError(ex, "MainWorker loop error");
				await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
			}
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
			procId = _instanceId // Guid worker instance id field
		};

		// Dapper: QuerySingleOrDefault returns a dynamic / map to a POCO
		var row = await conn.QuerySingleOrDefaultAsync<Event>(sqlClaimQuery, parameters);

		if (row == null) return null;

		//// Map dynamic to Event (careful with types)
		//var ev = new Event
		//{
		//	EventId = (Guid)row.EventId,
		//	Priority = (int)row.Priority,
		//	AggregateType = (string)row.AggregateType,
		//	AggregateId = (int)row.AggregateId,
		//	SequenceNumber = (long)row.SequenceNumber,
		//	EventType = (string)row.EventType,
		//	Payload = row.Payload == null ? Array.Empty<byte>() : (byte[])row.Payload,
		//	Status = (string)row.Status,
		//	Attempts = (int)row.Attempts,
		//	CreatedAt = (DateTime)row.CreatedAt,
		//	LastAttemptAt = row.LastAttemptAt == null ? (DateTime?)null : (DateTime)row.LastAttemptAt,
		//	ProcessorInstanceId = row.ProcessorInstanceId == null ? (Guid?)null : (Guid)row.ProcessorInstanceId
		//};

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
			catch (Exception ex) {  }

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
                    SET Status = @pending, ProcessorInstanceId = NULL, LastAttemptAt = NULL
                    WHERE Status = @processing AND LastAttemptAt < @cutoff";

		await conn.ExecuteAsync(sql, new { pending = "Pending", processing = "Processing", cutoff });
	}
}