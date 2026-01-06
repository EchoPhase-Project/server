using Cassandra;

namespace EchoPhase.DAL.Scylla
{
    public class Transaction : IDisposable
    {
        private readonly Database _database;
        private readonly BatchStatement _batch;
        private bool _completed;

        internal Transaction(Database database)
        {
            _database = database;
            _batch = new BatchStatement();
            _database.PushActiveTransaction(this);
        }

        internal void AddToBatch(BoundStatement bound)
        {
            if (_completed)
                throw new InvalidOperationException("Transaction already completed");
            _batch.Add(bound);
        }

        public void Commit()
        {
            if (_completed) throw new InvalidOperationException("Transaction already completed");

            if (!_batch.IsEmpty)
                _database.Session.Execute(_batch);

            _completed = true;
            _database.PopActiveTransaction();
        }

        public async Task CommitAsync()
        {
            if (_completed) throw new InvalidOperationException("Transaction already completed");

            if (!_batch.IsEmpty)
                await _database.Session.ExecuteAsync(_batch);

            _completed = true;
            _database.PopActiveTransaction();
        }

        public void Rollback()
        {
            if (_completed) throw new InvalidOperationException("Transaction already completed");

            _completed = true;
            _database.PopActiveTransaction();
        }

        public void Dispose()
        {
            if (!_completed)
                Rollback();
        }
    }
}
