// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Types.Extensions
{
    public static class FuncExtensions
    {
        public static Task<T> ToTask<T>(
                this Func<T> func) =>
            Task.Run(func);

        public static Func<Task<T>> ToAsync<T>(
                this Func<T> func) =>
            () => ToTask(func);
    }
}
