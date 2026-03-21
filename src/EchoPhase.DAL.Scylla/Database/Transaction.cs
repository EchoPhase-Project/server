// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Concurrent;
using Cassandra;

namespace EchoPhase.DAL.Scylla.Database
{
    public class Transaction : IDisposable
    {
        private readonly Database _database;
        private readonly List<BoundStatement> _statements;
        private readonly ConcurrentDictionary<string, PreparedStatement> _preparedCache;
        private bool _committed;
        private bool _disposed;
        private BatchType _batchType;

        internal Transaction(Database database, BatchType batchType = BatchType.Logged)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
            _statements = new List<BoundStatement>();
            _preparedCache = new ConcurrentDictionary<string, PreparedStatement>();
            _batchType = batchType;
            _database.PushActiveTransaction(this);
        }

        internal void AddStatement(string cql, params object[] parameters)
        {
            if (_committed)
                throw new InvalidOperationException("Cannot add statements to a committed transaction");

            if (_disposed)
                throw new ObjectDisposedException(nameof(Transaction));

            var prepared = _preparedCache.GetOrAdd(cql, _database.Session.Prepare);
            var bound = prepared.Bind(parameters);
            _statements.Add(bound);
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
            if (_committed)
                throw new InvalidOperationException("Transaction already committed");

            if (_disposed)
                throw new ObjectDisposedException(nameof(Transaction));

            try
            {
                if (_statements.Any())
                {
                    var batch = new BatchStatement();
                    batch.SetBatchType(_batchType);

                    foreach (var statement in _statements)
                    {
                        batch.Add(statement);
                    }

                    _database.Session.Execute(batch);
                }
                _committed = true;
            }
            finally
            {
                _database.PopActiveTransaction();
            }
        }

        public async Task CommitAsync()
        {
            if (_committed)
                throw new InvalidOperationException("Transaction already committed");

            if (_disposed)
                throw new ObjectDisposedException(nameof(Transaction));

            try
            {
                if (_statements.Any())
                {
                    var batch = new BatchStatement();
                    batch.SetBatchType(_batchType);

                    foreach (var statement in _statements)
                    {
                        batch.Add(statement);
                    }

                    await _database.Session.ExecuteAsync(batch);
                }
                _committed = true;
            }
            finally
            {
                _database.PopActiveTransaction();
            }
        }

        public void Rollback()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(Transaction));

            if (_committed)
                throw new InvalidOperationException("Cannot rollback a committed transaction");

            _statements.Clear();
            _database.PopActiveTransaction();
        }

        public void Clear()
        {
            if (_committed)
                throw new InvalidOperationException("Cannot clear a committed transaction");

            if (_disposed)
                throw new ObjectDisposedException(nameof(Transaction));

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
                }
            }

            _statements.Clear();
            _preparedCache.Clear();
            _disposed = true;
        }
    }
}
