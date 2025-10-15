using System.Data;
using GrpcRpcLib.Test.SyncDb.Shared.Abstractions;
using GrpcRpcLib.Test.SyncDb.Shared.CentralDbContextAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace GrpcRpcLib.Test.SyncDb.Shared.Implementations;

public class SqlServerSequenceProvider : ISequenceProvider
{
	private const string NativeProcName = "dbo.usp_GetNextAggregateSequence";

	public async Task<long> GetNextSequenceAsync(CentralDbContext db, string aggregateType, int aggregateId, CancellationToken ct = default)
	{
		if (db == null) throw new ArgumentNullException(nameof(db));

		var conn = db.Database.GetDbConnection();
		if (conn.State != ConnectionState.Open) await conn.OpenAsync(ct);

		var ambientTx = db.Database.CurrentTransaction?.GetDbTransaction();
		
		// 1) check if native proc exists
		bool nativeExists = false;
		try
		{
			await using var checkCmd = conn.CreateCommand();
			checkCmd.CommandText = "SELECT OBJECT_ID(@procName, 'P')";
			var p = checkCmd.CreateParameter();
			p.ParameterName = "@procName";
			p.Value = NativeProcName;
			checkCmd.Parameters.Add(p);
			checkCmd.Transaction=ambientTx;
			var scalar = await checkCmd.ExecuteScalarAsync(ct);
			nativeExists = scalar != null && scalar != DBNull.Value;
		}
		catch
		{
			nativeExists = false;
		}

		if (nativeExists)
		{
			// call natively compiled proc with output parameter
			await using var cmd = conn.CreateCommand();
			cmd.CommandText = NativeProcName;
			cmd.CommandType = CommandType.StoredProcedure;

			var pa = cmd.CreateParameter();
			pa.ParameterName = "@AggregateType";
			pa.Value = aggregateType;
			pa.DbType = DbType.String;
			cmd.Parameters.Add(pa);

			var pb = cmd.CreateParameter();
			pb.ParameterName = "@AggregateId";
			pb.Value = aggregateId;
			pb.DbType = DbType.Int32;
			cmd.Parameters.Add(pb);

			var pout = cmd.CreateParameter();
			pout.ParameterName = "@NewSeq";
			pout.DbType = DbType.Int64;
			pout.Direction = ParameterDirection.Output;
			cmd.Parameters.Add(pout);

			// Associate ambient transaction if present
		

			if (ambientTx != null)
			{
				cmd.Transaction = ambientTx;
			}

			await cmd.ExecuteNonQueryAsync(ct);

			var outVal = cmd.Parameters["@NewSeq"].Value;
			if (outVal == null || outVal == DBNull.Value) throw new InvalidOperationException("Native proc returned null.");
			return Convert.ToInt64(outVal);
		}
		else
		{
			// Fallback: MERGE
			var sql = @"
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
			await using var cmd2 = conn.CreateCommand();
			cmd2.CommandText = sql;

			

			var p1 = cmd2.CreateParameter();
			p1.ParameterName = "@aggType";
			p1.Value = aggregateType;
			cmd2.Parameters.Add(p1);

			var p2 = cmd2.CreateParameter();
			p2.ParameterName = "@aggId";
			p2.Value = aggregateId;
			cmd2.Parameters.Add(p2);

			var result = await cmd2.ExecuteScalarAsync(ct);
			if (result == null || result == DBNull.Value) throw new InvalidOperationException("MERGE returned null.");
			return Convert.ToInt64(result);
		}
	}
}
