//using GrpcRpcLib.Test.SyncDb.Shared.Abstractions;
//using GrpcRpcLib.Test.SyncDb.Shared.CentralDbContextAggregate;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.Extensions.DependencyInjection;

//namespace GrpcRpcLib.Test.SyncDb.Shared.Implementations;

//public class SqliteSequenceProvider : ISequenceProvider
//{
//	private readonly IServiceProvider _serviceProvider;

//	public SqliteSequenceProvider(IServiceProvider serviceProvider)
//	{
//		_serviceProvider = serviceProvider;
//	}

//	public async Task<long> GetNextSequenceAsync(string aggregateType, int aggregateId, CancellationToken ct = default)
//	{
//		await using var scope=_serviceProvider.CreateAsyncScope();

//		var db = scope.ServiceProvider.GetRequiredService<CentralDbContext>();

//		if (db == null) throw new ArgumentNullException(nameof(db));

//		// If ambient transaction exists, use it; otherwise create a serializable transaction
//		var ambient = db.Database.CurrentTransaction;
//		if (ambient != null)
//		{
//			return await GetNextSequenceInAmbientTransactionAsync(db, aggregateType, aggregateId, ct);
//		}

//		await using var tran = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
//		try
//		{
//			var next = await GetNextSequenceInAmbientTransactionAsync(db, aggregateType, aggregateId, ct);
//			await tran.CommitAsync(ct);
//			return next;
//		}
//		catch
//		{
//			await tran.RollbackAsync(ct);
//			throw;
//		}
//	}

//	private async Task<long> GetNextSequenceInAmbientTransactionAsync(CentralDbContext db, string aggregateType, int aggregateId, CancellationToken ct)
//	{
//		var existing = await db.AggregateSequences
//			.FirstOrDefaultAsync(a => a.AggregateType == aggregateType && a.AggregateId == aggregateId, ct);

//		if (existing != null)
//		{
//			existing.LastSequence += 1;
//			await db.SaveChangesAsync(ct);
//			return existing.LastSequence;
//		}
//		else
//		{
//			var inserted = new Entities.AggregateSequence
//			{
//				AggregateType = aggregateType,
//				AggregateId = aggregateId,
//				LastSequence = 1
//			};
//			db.AggregateSequences.Add(inserted);
//			await db.SaveChangesAsync(ct);
//			return 1;
//		}
//	}
//}