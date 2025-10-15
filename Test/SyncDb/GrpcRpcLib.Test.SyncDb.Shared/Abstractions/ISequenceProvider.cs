using GrpcRpcLib.Test.SyncDb.Shared.CentralDbContextAggregate;

namespace GrpcRpcLib.Test.SyncDb.Shared.Abstractions;

public interface ISequenceProvider
{
	/// <summary>
	/// Returns next sequence number for given aggregate type + id.
	/// Uses provided DbContext; may use ambient transaction or create its own as needed.
	/// </summary>
	Task<long> GetNextSequenceAsync(CentralDbContext db ,string aggregateType, int aggregateId, CancellationToken ct = default);
}