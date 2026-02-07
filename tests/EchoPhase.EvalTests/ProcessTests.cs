using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.EvalTests
{
    public class ProcessTests : IClassFixture<Fixture>
    {
        private readonly ILexer<TemplateToken> _lexer;
        private readonly IParser<TemplateToken> _parser;
		private readonly ITestOutputHelper _output;

        public ProcessTests(Fixture fixture, ITestOutputHelper output)
        {
            _lexer = fixture.Provider.GetRequiredService<ILexer<TemplateToken>>();
            _parser = fixture.Provider.GetRequiredService<IParser<TemplateToken>>();
			_output = output;
        }

        [Fact]
        public void Process_ReplacesVariableSubstitution_ReturnsCorrectResult()
        {
            var variables = new Dictionary<string, object>
            {
                ["name"] = "Alice",
                ["age"] = 30
            };

            var input = "Hello, @name! You are @age years old.";
            var expected = "Hello, Alice! You are 30 years old.";

            var result = Eval.Process<string>(_lexer, _parser, input, variables);

            result.OnFailure(err => Assert.Fail($"Evaluation failed: {err.Message}"));
            result.OnSuccess(value => Assert.Equal(expected, value));
        }

        [Fact]
        public void Process_EvaluatesExpression_ReturnsCorrectResult()
        {
            var variables = new Dictionary<string, object>
            {
                ["a"] = 5,
                ["b"] = 10
            };

            var input = "Sum: @(a + b), product: @(a * b)";
            var expected = "Sum: 15, product: 50";

			var result = Eval.Process<string>(_lexer, _parser, input, variables);

			result.OnFailure(err => Assert.Fail($"Evaluation failed: {err.Message}"));
			result.OnSuccess(value => Assert.Equal(expected, value));
        }

        [Fact]
        public void Process_MixedVariableAndExpressionSubstitution_ReturnsCorrectResult()
        {
            var variables = new Dictionary<string, object>
            {
                ["x"] = 2,
                ["y"] = 3
            };

            var input = "Variables: @x and @y, sum: @(x + y)";
            var expected = "Variables: 2 and 3, sum: 5";

            var result = Eval.Process<string>(_lexer, _parser, input, variables);

            result.OnFailure(err => Assert.Fail($"Evaluation failed: {err.Message}"));
            result.OnSuccess(value => Assert.Equal(expected, value));
        }

        [Fact]
        public void Process_HandlesNestedExpressionInAtParentheses_ReturnsCorrectResult()
        {
            var variables = new Dictionary<string, object> { ["val"] = 4 };
            var input = "Square plus one: @((val * val) + 1)";
            var expected = "Square plus one: 17";

            var result = Eval.Process<string>(_lexer, _parser, input, variables);

            result.OnFailure(err => Assert.Fail($"Evaluation failed: {err.Message}"));
            result.OnSuccess(value => Assert.Equal(expected, value));
        }

        [Fact]
        public void Process_EscapesAtSymbols_ReturnsCorrectResult()
        {
            var variables = new Dictionary<string, object> { ["test"] = "value" };
            var input = "This is @@escaped and \\@also escaped, real: @test";
            var expected = "This is @escaped and @also escaped, real: value";

            var result = Eval.Process<string>(_lexer, _parser, input, variables);

            result.OnFailure(err => Assert.Fail($"Evaluation failed: {err.Message}"));
            result.OnSuccess(value => Assert.Equal(expected, value));
        }

        [Fact]
        public void Process_UnknownVariable_ReturnsError()
        {
            var variables = new Dictionary<string, object>();
            var input = "Unknown variable: @unknown";
            var expected = "KeyNotFound";

            var result = Eval.Process<string>(_lexer, _parser, input, variables);

            result.OnFailure(err => Assert.Contains(expected, err.Code));
            result.OnSuccess(value => Assert.Fail($"Evaluation succeeded"));
        }

        [Fact]
        public void Process_InvalidExpression_ReturnsError()
        {
            var variables = new Dictionary<string, object>();
            var input = "Invalid expression: @(1 + )";
            var expected = "NotSupported";

            var result = Eval.Process<string>(_lexer, _parser, input, variables);

            result.OnFailure(err => Assert.Contains(expected, err.Code));
            result.OnSuccess(value => Assert.Fail($"Evaluation succeeded"));
        }

        [Theory]
        [InlineData("Secret: @test", "Secret: @secret", "@secret")]
        [InlineData("Secret: @test", "Secret: @(secret)", "@(secret)")]
        public void Process_VariableExpressionInjection_ReturnsShieldedString(string expr, string expected, string inj)
        {
            var variables = new Dictionary<string, object>
            {
                ["secret"] = "qwerty",
                ["test"] = inj,
            };

            var result = Eval.Process<string>(_lexer, _parser, expr, variables);

            result.OnFailure(err => Assert.Fail($"Evaluation failed: {err.Message}"));
            result.OnSuccess(value => Assert.Equal(expected, value));
        }
    }
}
