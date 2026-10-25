// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Concurrent;
using Cassandra;
using EchoPhase.DAL.Scylla.Interfaces;

namespace EchoPhase.DAL.Scylla.Database
{
    /// <summary>
    /// CQL on the cluster through the DataStax driver: statements with values
    /// are prepared once per connection and bound.
    /// </summary>
    public sealed class DriverCqlEngine : ICqlEngine
    {
        private readonly ISession _session;
        private readonly IQueryLogger? _logger;
        private readonly ConcurrentDictionary<string, PreparedStatement> _prepared = new();

        public DriverCqlEngine(ISession session, IQueryLogger? logger = null)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _logger = logger;
        }

        public CqlResult Execute(CqlStatement statement)
            => Result(_session.Execute(Bind(statement.Values.Length > 0 ? Prepare(statement.Cql) : null, statement)), statement);

        public async Task<CqlResult> ExecuteAsync(CqlStatement statement, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var prepared = statement.Values.Length > 0 ? await PrepareAsync(statement.Cql, ct) : null;
            // Driver 3.x has no cancellable ExecuteAsync: WaitAsync releases the
            // caller on cancellation; the server-side request still completes.
            var rows = await _session.ExecuteAsync(Bind(prepared, statement)).WaitAsync(ct);
            return Result(rows, statement);
        }

        public void ExecuteBatch(IReadOnlyList<CqlStatement> statements, BatchType type)
        {
            if (statements.Count > 0)
                _session.Execute(BuildBatch(statements, type));
        }

        public async Task ExecuteBatchAsync(IReadOnlyList<CqlStatement> statements, BatchType type, CancellationToken ct = default)
        {
            if (statements.Count > 0)
                await _session.ExecuteAsync(BuildBatch(statements, type)).WaitAsync(ct);
        }

        private PreparedStatement? Prepare(string cql)
            => _prepared.GetOrAdd(cql, _session.Prepare);

        private async Task<PreparedStatement> PrepareAsync(string cql, CancellationToken ct)
        {
            if (_prepared.TryGetValue(cql, out var prepared))
                return prepared;
            prepared = await _session.PrepareAsync(cql).WaitAsync(ct);
            return _prepared.GetOrAdd(cql, prepared);
        }

        private static IStatement Bind(PreparedStatement? prepared, CqlStatement statement)
        {
            IStatement bound = statement.Values.Length > 0 && prepared is not null
                ? prepared.Bind(statement.Values)
                : new SimpleStatement(statement.Cql);
            bound.SetIdempotence(statement.Idempotent);
            if (statement.Options is { } options)
            {
                if (options.Consistency is { } consistency)
                    bound.SetConsistencyLevel(consistency);
                if (options.SerialConsistency is { } serial)
                    bound.SetSerialConsistencyLevel(serial);
                if (options.Timeout is { } timeout)
                    bound.SetReadTimeoutMillis((int)timeout.TotalMilliseconds);
            }
            if (statement.PageSize is { } size)
                bound.SetPageSize(size).SetAutoPage(false).SetPagingState(statement.PagingState);
            return bound;
        }

        // A page reads only the rows received; otherwise every page is fetched.
        private static CqlResult Result(RowSet rows, CqlStatement statement)
            => statement.PageSize is null
                ? new CqlResult(rows.ToList(), null)
                : new CqlResult(rows.Take(rows.GetAvailableWithoutFetching()).ToList(), rows.PagingState);

        private BatchStatement BuildBatch(IReadOnlyList<CqlStatement> statements, BatchType type)
        {
            var batch = new BatchStatement();
            batch.SetBatchType(type);
            var partitions = new HashSet<string>();
            foreach (var statement in statements)
            {
                var bound = Prepare(statement.Cql)!.Bind(statement.Values);
                batch.Add(bound);
                partitions.Add(TableOf(statement.Cql) + ":" +
                    (bound.RoutingKey?.RawRoutingKey is { } key ? Convert.ToBase64String(key) : "?"));
            }

            // A batch can be retried only when every statement in it can.
            batch.SetIdempotence(type != BatchType.Counter && statements.All(s => s.Idempotent));

            // An unlogged batch over several partitions gives no atomicity and makes
            // one coordinator fan out to every replica set; separate writes are faster.
            if (type == BatchType.Unlogged && partitions.Count > 1)
                _logger?.LogWarning(
                    $"Unlogged batch spans {partitions.Count} partitions: it is not atomic and adds coordinator load. " +
                    "Use separate writes, or a Logged batch if atomicity is required.");
            return batch;
        }

        private static string TableOf(string cql)
            => System.Text.RegularExpressions.Regex.Match(cql, @"\b(?:INTO|UPDATE|FROM)\s+([\w.""]+)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase) is { Success: true } m ? m.Groups[1].Value : string.Empty;

        public void Dispose() => _prepared.Clear();
    }
}
