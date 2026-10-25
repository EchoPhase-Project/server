// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

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
    public class Batch : IDisposable
    {
        private readonly Database _database;
        private readonly List<CqlStatement> _statements = new();
        private bool _committed;
        private bool _disposed;
        private BatchType _batchType;

        internal Batch(Database database, BatchType batchType = BatchType.Unlogged)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
            _batchType = batchType;
            _database.PushActiveBatch(this);
        }

        internal void AddStatement(string cql, object?[] parameters, bool idempotent)
        {
            if (_committed)
                throw new InvalidOperationException("Cannot add statements to a committed batch");

            if (_disposed)
                throw new ObjectDisposedException(nameof(Batch));

            _statements.Add(new CqlStatement(cql, parameters, idempotent));
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
                    _database.Executor.ExecuteBatch(_statements, _batchType);

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
                    await _database.Executor.ExecuteBatchAsync(_statements, _batchType, ct);

                _committed = true;
            }
            finally
            {
                _database.PopActiveBatch();
            }
        }

        private void EnsureUsable()
        {
            if (_committed)
                throw new InvalidOperationException("Batch already committed");

            if (_disposed)
                throw new ObjectDisposedException(nameof(Batch));
        }

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
