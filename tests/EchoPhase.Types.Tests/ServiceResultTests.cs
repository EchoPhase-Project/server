// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Types.Result;

namespace EchoPhase.Types.Tests
{
    public class ServiceResultTests
    {
        private static readonly IServiceError NotFound = new ServiceError("NotFound", "No such thing.");

        [Fact]
        public void Map_PassesANullValueOfASuccess()
        {
            var result = ServiceResult<string?>.Success(null).Map(v => v ?? "none");

            Assert.True(result.Successful);
            Assert.Equal("none", result.Value);
        }

        [Fact]
        public void Bind_OnANullSuccess_RunsTheNextStep()
        {
            var result = ServiceResult<string?>.Success(null)
                .Bind(_ => ServiceResult<int>.Success(1));

            Assert.True(result.Successful);
            Assert.Equal(1, result.Value);
        }

        [Fact]
        public void Failure_SkipsTheStepsAndKeepsItsError()
        {
            var calls = 0;
            var result = ServiceResult<int>.Failure(NotFound)
                .Map(v => { calls++; return v + 1; })
                .Bind(v => { calls++; return ServiceResult<string>.Success(v.ToString()); });

            Assert.False(result.Successful);
            Assert.Same(NotFound, result.Error);
            Assert.Equal(0, calls);
        }

        [Fact]
        public void Exceptions_Propagate()
        {
            var success = ServiceResult<int>.Success(1);

            Assert.Throws<InvalidOperationException>(() =>
                success.Map<int>(_ => throw new InvalidOperationException("bug")));
            Assert.Throws<InvalidOperationException>(() =>
                success.Bind<int>(_ => throw new InvalidOperationException("bug")));
        }

        [Fact]
        public async Task BindAsync_LetsCancellationThrough()
        {
            var success = ServiceResult<int>.Success(1);

            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                success.BindAsync<int>(_ => throw new OperationCanceledException()));
        }

        [Fact]
        public void Error_IsReadOnlyOnceBuilt()
        {
            var error = new ServiceError(e => e.Set("Invalid", "Bad input."));

            Assert.Equal("Invalid", error.Code);
            Assert.Throws<InvalidOperationException>(() => error.Set("Other", "Changed."));
            Assert.Throws<InvalidOperationException>(() => new ServiceError("A", "B").SetCode("C"));
        }
    }
}
