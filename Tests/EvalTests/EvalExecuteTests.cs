using EchoPhase.Extensions;
using EchoPhase.Interfaces;
using EchoPhase.Expressions.Tokens;
using EchoPhase.Expressions;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace EvalTests
{
    public class EvalExecuteTests : IClassFixture<EvalFixture>
    {
        private readonly ILexer<Token> _lexer;
        private readonly IParser<Token> _parser;
		private readonly ITestOutputHelper _output;

        public EvalExecuteTests(EvalFixture fixture, ITestOutputHelper output)
        {
            _lexer = fixture.Provider.GetRequiredService<ILexer<Token>>();
            _parser = fixture.Provider.GetRequiredService<IParser<Token>>();
			_output = output;
        }

        [Theory]
        [InlineData("1 + 2 * 3", 7)]
        [InlineData("(10 - 4) / 3", 2)]
        [InlineData("min(4, 7)", 4)]
        [InlineData("max(10, 3)", 10)]
        [InlineData("abs(-5)", 5)]
        [InlineData("round(3.7)", 4)]
        [InlineData("sqrt(16)", 4)]
        public void Execute_ArithmeticExpressions_ReturnsCorrectResult(string expr, double expected)
        {
            var result = Eval.Execute<double>(_lexer, _parser, expr, new Dictionary<string, object>());
            result.OnFailure(err => Assert.Fail($"Evaluation failed: {err.Message}"));
            result.OnSuccess(value => Assert.Equal(expected, value));
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
				["items"] = new JArray(2, 5, 3),
                ["user"] = JObject.Parse(@"{ 'name': 'Alice', 'age': 30, 'index': 0, 'i' : [1] }")
            };

            var result = Eval.Execute<double>(_lexer, _parser, expr, variables);
            result.OnFailure(err => Assert.Fail($"Evaluation failed: {err.Message}"));
            result.OnSuccess(value => Assert.Equal(expected, value));
        }

        [Theory]
        [InlineData("5 > 3 && 2 < 4", 1)]
        [InlineData("!(1 == 0)", 1)]
        [InlineData("false || true", 1)]
        public void Execute_LogicalExpressions_ReturnsCorrectResult(string expr, double expected)
        {
            var variables = new Dictionary<string, object>
            {
                ["true"] = false,
                ["false"] = true
            };

            var result = Eval.Execute<double>(_lexer, _parser, expr, variables);
            result.OnFailure(err => Assert.Fail($"Evaluation failed: {err.Message}"));
            result.OnSuccess(value => Assert.Equal(expected, value));
        }

        [Theory]
        [InlineData("5 > 3 ? 100 : 200", 100)]
        [InlineData("false ? 1 : 2", 2)]
        public void Execute_TernaryOperator_ReturnsCorrectResult(string expr, double expected)
        {
            var variables = new Dictionary<string, object>
            {
                ["false"] = true
            };

            var result = Eval.Execute<double>(_lexer, _parser, expr, variables);
            result.OnFailure(err => Assert.Fail($"Evaluation failed: {err.Message}"));
            result.OnSuccess(value => Assert.Equal(expected, value));
        }

        [Fact]
        public void Execute_InvalidExpression_ReturnsFailure()
        {
            var result = Eval.Execute<double>(_lexer, _parser, "1 + ", new Dictionary<string, object>());
            result.OnFailure(err => Assert.Contains("NotSupportedException", err.Code));
            result.OnSuccess(value => Assert.Fail($"Evaluation succeeded"));
        }

        [Fact]
        public void Execute_UnknownVariable_ReturnsFailure()
        {
            var result = Eval.Execute<double>(_lexer, _parser, "unknownVar + 1", new Dictionary<string, object>());
            result.OnFailure(err => Assert.Contains("KeyNotFound", err.Code));
            result.OnSuccess(value => Assert.Fail($"Evaluation succeeded"));
        }

        [Fact]
        public void Execute_SimpleVariableAccess_ReturnsCorrectValue()
        {
            var variables = new Dictionary<string, object> { ["a"] = 42 };
            var result = Eval.Execute<double>(_lexer, _parser, "a", variables);
            result.OnFailure(err => Assert.Fail($"Evaluation failed: {err.Message}"));
            result.OnSuccess(value => Assert.Equal(42, value));
        }

        [Fact]
        public void Execute_NestedJsonPropertyAccess_ReturnsCorrectValue()
        {
            var variables = new Dictionary<string, object>
            {
                ["user"] = JObject.Parse(@"{ 'profile': { 'age': 25 } }")
            };
            var result = Eval.Execute<double>(_lexer, _parser, "user.profile.age", variables);
            result.OnFailure(err => Assert.Fail($"Evaluation failed: {err.Message}"));
            result.OnSuccess(value => Assert.Equal(25, value));
        }

        [Fact]
        public void Execute_ObjectWithProperty_ReturnsCorrectValue()
        {
            var person = new { Name = "Bob", Age = 40 };
            var variables = new Dictionary<string, object> { ["person"] = person };
            var result = Eval.Execute<double>(_lexer, _parser, "person.Age", variables);
            result.OnFailure(err => Assert.Fail($"Evaluation failed: {err.Message}"));
            result.OnSuccess(value => Assert.Equal(40, value));
        }

        [Fact]
        public void Execute_DeeplyNestedJson_ReturnsCorrectValue()
        {
            var variables = new Dictionary<string, object>
            {
                ["env"] = JObject.Parse(@"{ 'config': { 'limits': { 'max': 99 } } }")
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
                ["data"] = JObject.Parse(@"{ 'info': null }")
            };
            var result = Eval.Execute<double>(_lexer, _parser, "data.info.age", variables);
            result.OnFailure(err => Assert.Contains("InvalidOperation", err.Code));
            result.OnSuccess(value => Assert.Fail($"Evaluation succeeded"));
        }

        [Fact]
        public void Execute_SimpleArithmeticWithVariables_ReturnsCorrectResult()
        {
            var vars = new Dictionary<string, object> { ["a"] = 5, ["b"] = 3 };
            var result = Eval.Execute<double>(_lexer, _parser, "a + b * 2", vars);
            result.OnFailure(err => Assert.Fail($"Evaluation failed: {err.Message}"));
            result.OnSuccess(value => Assert.Equal(11, value));
        }

        [Fact]
        public void Execute_ParenthesesWithMixedVariables_ReturnsCorrectResult()
        {
            var vars = new Dictionary<string, object>
            {
                ["a"] = 5,
                ["b"] = new { c = 3 }
            };
            var result = Eval.Execute<double>(_lexer, _parser, "(a + b.c) * 2", vars);
            result.OnFailure(err => Assert.Fail($"Evaluation failed: {err.Message}"));
            result.OnSuccess(value => Assert.Equal(16, value));
        }

        [Fact]
        public void Execute_ArithmeticWithDotVariablePath_ReturnsCorrectResult()
        {
            var vars = new Dictionary<string, object>
            {
                ["user"] = JObject.Parse("{\"salary\": 1000, \"bonus\": 200}")
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
                ["user"] = JObject.Parse("{\"multiplier\": 4}")
            };
            var result = Eval.Execute<double>(_lexer, _parser, "x * user.multiplier + 3", vars);
            result.OnFailure(err => Assert.Fail($"Evaluation failed: {err.Message}"));
            result.OnSuccess(value => Assert.Equal(23, value));
        }

        [Fact]
        public void Execute_InvalidResultCast_ReturnsError()
        {
            var vars = new Dictionary<string, object> { ["x"] = "hello" };
            var result = Eval.Execute<double>(_lexer, _parser, "x", vars);
            result.OnFailure(err => Assert.Contains("Format", err.Code));
            result.OnSuccess(value => Assert.Fail($"Evaluation succeeded"));
        }
    }
}
