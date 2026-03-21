// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.Scripting.Tests
{
    public class ExecuteTests : IClassFixture<Fixture>
    {
        private readonly ILexer<Token> _lexer;
        private readonly IParser<Token> _parser;
        private readonly ITestOutputHelper _output;

        public ExecuteTests(Fixture fixture, ITestOutputHelper output)
        {
            _lexer = fixture.Provider.GetRequiredService<ILexer<Token>>();
            _parser = fixture.Provider.GetRequiredService<IParser<Token>>();
            _output = output;
        }

        [Theory]
        [InlineData("x + 5", 7)]
        [InlineData("items[x] + x", 5)]
        [InlineData("items[user.index] + x", 4)]
        [InlineData("items[1]", 5)]
        [InlineData("items[user.i[0]]", 5)]
        [InlineData("items[user.index+2]", 3)]
        [InlineData("user.age >= 18 ? 8 : 4", 8)]
        [InlineData("user.name == 'Alice' ? 8 : 4", 8)]
        public void Execute_VariablesAndNestedProperties_ReturnsCorrectResult(string expr, double expected)
        {
            var variables = new Dictionary<string, object>
            {
                ["x"] = 2,
                ["items"] = new List<object?> { 2, 5, 3 },
                ["user"] = new Dictionary<string, object?>
                {
                    ["name"] = "Alice",
                    ["age"] = 30,
                    ["index"] = 0,
                    ["i"] = new List<object?> { 1 }
                }
            };

            var result = Eval.Execute<double>(_lexer, _parser, expr, variables);
            result.OnFailure(err => Assert.Fail($"Evaluation failed: {err.Message}"));
            result.OnSuccess(value => Assert.Equal(expected, value));
        }

        [Fact]
        public void Execute_NestedJsonPropertyAccess_ReturnsCorrectValue()
        {
            var variables = new Dictionary<string, object>
            {
                ["user"] = new Dictionary<string, object?>
                {
                    ["profile"] = new Dictionary<string, object?> { ["age"] = 25 }
                }
            };
            var result = Eval.Execute<double>(_lexer, _parser, "user.profile.age", variables);
            result.OnFailure(err => Assert.Fail($"Evaluation failed: {err.Message}"));
            result.OnSuccess(value => Assert.Equal(25, value));
        }

        [Fact]
        public void Execute_DeeplyNestedJson_ReturnsCorrectValue()
        {
            var variables = new Dictionary<string, object>
            {
                ["env"] = new Dictionary<string, object?>
                {
                    ["config"] = new Dictionary<string, object?>
                    {
                        ["limits"] = new Dictionary<string, object?> { ["max"] = 99 }
                    }
                }
            };
            var result = Eval.Execute<double>(_lexer, _parser, "env.config.limits.max", variables);
            result.OnFailure(err => Assert.Fail($"Evaluation failed: {err.Message}"));
            result.OnSuccess(value => Assert.Equal(99, value));
        }

        [Fact]
        public void Execute_NullDotPath_ThrowsFriendlyError()
        {
            var variables = new Dictionary<string, object>
            {
                ["data"] = new Dictionary<string, object?> { ["info"] = null }
            };
            var result = Eval.Execute<double>(_lexer, _parser, "data.info.age", variables);
            result.OnFailure(err => Assert.True(
                err.Code.Contains("InvalidOperation") || err.Code.Contains("KeyNotFound"),
                $"Unexpected error code: {err.Code}"));
            result.OnSuccess(value => Assert.Fail($"Evaluation succeeded"));
        }

        [Fact]
        public void Execute_ArithmeticWithDotVariablePath_ReturnsCorrectResult()
        {
            var vars = new Dictionary<string, object>
            {
                ["user"] = new Dictionary<string, object?>
                {
                    ["salary"] = 1000,
                    ["bonus"] = 200
                }
            };
            var result = Eval.Execute<double>(_lexer, _parser, "user.salary + user.bonus", vars);
            result.OnFailure(err => Assert.Fail($"Evaluation failed: {err.Message}"));
            result.OnSuccess(value => Assert.Equal(1200, value));
        }

        [Fact]
        public void Execute_MixedVariableTypesArithmetic_ReturnsCorrectResult()
        {
            var vars = new Dictionary<string, object>
            {
                ["x"] = 5,
                ["user"] = new Dictionary<string, object?> { ["multiplier"] = 4 }
            };
            var result = Eval.Execute<double>(_lexer, _parser, "x * user.multiplier + 3", vars);
            result.OnFailure(err => Assert.Fail($"Evaluation failed: {err.Message}"));
            result.OnSuccess(value => Assert.Equal(23, value));
        }
    }
}
