// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Scripting.Engine
{
    /// <summary>
    /// Work one evaluation may do: lambda calls and produced elements.
    /// Evaluation is synchronous, so a per-thread counter follows it exactly;
    /// map(xs, x =&gt; xs) over a large input stops here instead of holding a
    /// worker for minutes.
    /// </summary>
    internal static class Budget
    {
        public const int MaxOperations = 1_000_000;

        [ThreadStatic]
        private static int _spent;

        [ThreadStatic]
        private static int _depth;

        /// <summary>Runs one top-level evaluation with a fresh budget (nested calls share the outer one).</summary>
        public static T Run<T>(Func<T> evaluate)
        {
            if (_depth++ == 0)
                _spent = 0;
            try
            {
                return evaluate();
            }
            finally
            {
                _depth--;
            }
        }

        /// <summary>
        /// Runs apart from any evaluation in progress, with its own smaller
        /// budget (parse-time folding), and leaves that evaluation's count as it was.
        /// </summary>
        public static T Isolated<T>(int operations, Func<T> evaluate)
        {
            var (spent, depth) = (_spent, _depth);
            _spent = MaxOperations - operations;
            _depth = 1;
            try
            {
                return evaluate();
            }
            finally
            {
                (_spent, _depth) = (spent, depth);
            }
        }

        public static void Spend(int operations)
        {
            // Outside an evaluation (Run/Isolated) it is the server's own code
            // calling a helper, not a user's expression: nothing to limit, and
            // no evaluation would reset what it counted.
            if (_depth == 0)
                return;

            _spent += operations;
            if (_spent > MaxOperations)
                throw new InvalidOperationException($"Expression exceeded {MaxOperations} operations.");
        }
    }
}
