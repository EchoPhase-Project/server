using EchoPhase.DAL.Scylla.Cql;
using EchoPhase.DAL.Scylla.Interfaces;

namespace EchoPhase.DAL.Scylla.Loggers
{
    public class ConsoleQueryLogger : IQueryLogger
    {
        private readonly bool _enableTiming;
        private readonly bool _formatCql;

        public ConsoleQueryLogger(bool enableTiming = true, bool formatCql = true)
        {
            _enableTiming = enableTiming;
            _formatCql = formatCql;
        }

        public void LogQuery(string cql, Type resultType, object[] parameters)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            Console.WriteLine($"[{timestamp}] Executing query (returning {resultType.Name})");

            if (parameters.Length > 0)
            {
                Console.WriteLine($"      Parameters: [{string.Join(", ", parameters)}]");
            }

            var formattedCql = _formatCql ? CqlFormatter.Format(cql) : cql;
            Console.WriteLine(formattedCql);
        }

        public void LogQueryTiming(long elapsedMs)
        {
            if (_enableTiming)
            {
                Console.WriteLine($"      ✓ Executed in {elapsedMs}ms\n");
            }
        }

        public void LogError(string cql, Exception ex)
        {
            Console.WriteLine($"      ✗ Query failed: {ex.Message}");
            Console.WriteLine($"      Query: {cql}");
        }
    }
}
