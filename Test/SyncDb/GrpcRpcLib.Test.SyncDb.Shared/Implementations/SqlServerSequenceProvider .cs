using System.Data;
using GrpcRpcLib.Test.SyncDb.Shared.Abstractions;
using GrpcRpcLib.Test.SyncDb.Shared.CentralDbContextAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GrpcRpcLib.Test.SyncDb.Shared.Implementations;

public class SqlServerSequenceProvider : ISequenceProvider
{
	// MERGE with OUTPUT inserted.LastSequence is fine for disk-based table and is atomic within DB transaction
	private const string SqlCommand = @"
										MERGE dbo.AggregateSequences AS target
										USING (VALUES (@aggType, @aggId)) AS source(AggregateType, AggregateId)
										ON (target.AggregateType = source.AggregateType AND target.AggregateId = source.AggregateId)
										WHEN MATCHED THEN
										    UPDATE SET LastSequence = target.LastSequence + 1
										WHEN NOT MATCHED THEN
										    INSERT (AggregateType, AggregateId, LastSequence)
										    VALUES (source.AggregateType, source.AggregateId, 1)
										OUTPUT inserted.LastSequence;
										";

	public async Task<long> GetNextSequenceAsync(CentralDbContext db, string aggregateType, int aggregateId, CancellationToken ct = default)
	{

		if (db == null) throw new ArgumentNullException(nameof(db));

		if (string.IsNullOrEmpty(aggregateType)) throw new ArgumentNullException(nameof(aggregateType));

		
		var conn = db.Database.GetDbConnection();

		if (conn.State != ConnectionState.Open) await conn.OpenAsync(ct);

		// if caller has a current transaction, attach command to it so MERGE participates in same transaction
		var ambientTx = db.Database.CurrentTransaction?.GetDbTransaction();

		await using var cmd = conn.CreateCommand();

		cmd.CommandText = SqlCommand;

		cmd.CommandType = CommandType.Text;

		if (ambientTx != null) cmd.Transaction = ambientTx;
		
		var p1 = cmd.CreateParameter();

		p1.ParameterName = "@aggType";
		p1.Value = aggregateType;
		p1.DbType = DbType.String;
		cmd.Parameters.Add(p1);

		var p2 = cmd.CreateParameter();
		p2.ParameterName = "@aggId";
		p2.Value = aggregateId;
		p2.DbType = DbType.Int32;
		cmd.Parameters.Add(p2);

		var scalar = await cmd.ExecuteScalarAsync(ct);

		if (scalar == null || scalar == DBNull.Value)
			throw new InvalidOperationException("Failed to obtain next sequence.");

		return Convert.ToInt64(scalar);
	}
}
