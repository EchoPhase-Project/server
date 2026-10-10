// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Collections;
using System.Linq.Expressions;

namespace EchoPhase.DAL.Scylla.Linq
{
    public class ScyllaQueryable<T> : IOrderedQueryable<T>
    {
        private readonly QueryProvider _provider;

        public ScyllaQueryable(QueryProvider provider, Expression expression)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        }

        public Type ElementType => typeof(T);

        public Expression Expression { get; }

        public IQueryProvider Provider => _provider;

        public IEnumerator<T> GetEnumerator() =>
            _provider.Execute<IEnumerable<T>>(Expression).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
