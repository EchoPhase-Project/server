// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.RegularExpressions;
using Cassandra;

namespace EchoPhase.DAL.Scylla.Database
{
    /// <summary>
    /// A CQL batch: statements sent together. This is not a transaction —
    /// there is no isolation or rollback on the server. A <c>LOGGED</c> batch is
    /// atomic (all or nothing, eventually) at the cost of an extra batchlog
    /// write; an <c>UNLOGGED</c> one is not atomic and is only worth it for
    /// several writes to the same partition.
    /// </summary>
    public partial class Batch : IDisposable
    {
        private readonly Database _database;
        private readonly List<(BoundStatement Statement, string Table, bool Idempotent)> _statements = new();
        private bool _committed;
        private bool _disposed;
        private BatchType _batchType;

        internal Batch(Database database, BatchType batchType = BatchType.Unlogged)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
            _batchType = batchType;
            _database.PushActiveBatch(this);
        }

        internal void AddStatement(string cql, object[] parameters, bool idempotent)
        {
            if (_committed)
                throw new InvalidOperationException("Cannot add statements to a committed batch");

            if (_disposed)
                throw new ObjectDisposedException(nameof(Batch));

            var prepared = _database.Executor.Prepare(cql);
            _statements.Add((prepared.Bind(parameters), TableOf(cql), idempotent));
        }

        public int StatementCount => _statements.Count;

        public BatchType BatchType
        {
            get => _batchType;
            set
            {
                if (_committed)
                    throw new InvalidOperationException("Cannot change batch type after commit");
                _batchType = value;
            }
        }

        public void Commit()
        {
            EnsureUsable();

            try
            {
                if (_statements.Count > 0)
                    _database.Session.Execute(BuildBatch());

                _committed = true;
            }
            finally
            {
                _database.PopActiveBatch();
            }
        }

        public async Task CommitAsync(CancellationToken ct = default)
        {
            EnsureUsable();

            try
            {
                if (_statements.Count > 0)
                    // Driver 3.x: no cancellable ExecuteAsync; WaitAsync releases the caller.
                    await _database.Session.ExecuteAsync(BuildBatch()).WaitAsync(ct);

                _committed = true;
            }
            finally
            {
                _database.PopActiveBatch();
            }
        }

        private BatchStatement BuildBatch()
        {
            WarnIfMisused();

            var batch = new BatchStatement();
            batch.SetBatchType(_batchType);

            foreach (var (statement, _, _) in _statements)
                batch.Add(statement);

            // A batch can be retried only when every statement in it can.
            batch.SetIdempotence(_batchType != BatchType.Counter && _statements.All(s => s.Idempotent));
            return batch;
        }

        /// <summary>
        /// An unlogged batch over several partitions gives no atomicity and makes
        /// one coordinator fan out to every replica set; separate writes are
        /// faster. Logged batches across partitions are legitimate but costly.
        /// </summary>
        private void WarnIfMisused()
        {
            if (_batchType != BatchType.Unlogged)
                return;

            var partitions = _statements
                .Select(s => s.Table + ":" + (s.Statement.RoutingKey?.RawRoutingKey is { } key
                    ? Convert.ToBase64String(key)
                    : "?"))
                .Distinct()
                .Count();

            if (partitions > 1)
                _database.Executor.Logger?.LogWarning(
                    $"Unlogged batch spans {partitions} partitions: it is not atomic and adds coordinator load. " +
                    "Use separate writes, or a Logged batch if atomicity is required.");
        }

        private void EnsureUsable()
        {
            if (_committed)
                throw new InvalidOperationException("Batch already committed");

            if (_disposed)
                throw new ObjectDisposedException(nameof(Batch));
        }

        [GeneratedRegex(@"\b(?:INTO|UPDATE|FROM)\s+([\w.""]+)", RegexOptions.IgnoreCase)]
        private static partial Regex TableRegex();

        private static string TableOf(string cql)
            => TableRegex().Match(cql) is { Success: true } m ? m.Groups[1].Value : string.Empty;

        public void Rollback()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(Batch));

            if (_committed)
                throw new InvalidOperationException("Cannot discard a committed batch");

            // Nothing was sent yet, so dropping the statements is the whole rollback.
            _statements.Clear();
            _database.PopActiveBatch();
        }

        public void Clear()
        {
            EnsureUsable();
            _statements.Clear();
        }

        public void Dispose()
        {
            if (_disposed) return;

            if (!_committed)
            {
                try
                {
                    Rollback();
                }
                catch
                {
                    // Dispose must not throw; an uncommitted batch is simply discarded.
                }
            }

            _statements.Clear();
            _disposed = true;
        }
    }
}
