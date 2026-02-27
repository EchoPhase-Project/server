using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.EvalTests
{
    public class Person
    {
        public string Name { get; set; } = "";
        public int Age
        {
            get; set;
        }
        public Address Address { get; set; } = new();
    }

    public class Address
    {
        public string City { get; set; } = "";
        public int Zip
        {
            get; set;
        }
    }

    public class TemplateModelTests : IClassFixture<Fixture>
    {
        private readonly ILexer<TemplateToken> _lexer;
        private readonly IParser<TemplateToken> _parser;
        private readonly ITestOutputHelper _output;

        public TemplateModelTests(Fixture fixture, ITestOutputHelper output)
        {
            _lexer = fixture.Provider.GetRequiredService<ILexer<TemplateToken>>();
            _parser = fixture.Provider.GetRequiredService<IParser<TemplateToken>>();
            _output = output;
        }

        [Fact]
        public void ProcessTemplate_SimpleReplacement_Works()
        {
            var person = new Person { Name = "Alice", Age = 30 };
            string input = "Hello, @Name! You are @Age years old.";
            var expected = "Hello, Alice! You are 30 years old.";

            var result = Eval.ProcessTemplate<Person, string>(_lexer, _parser, input, person);

            result.OnFailure(err => Assert.Fail($"Evaluation failed: {err.Message}"));
            result.OnSuccess(value => Assert.Equal(expected, value));
        }

        [Fact]
        public void ProcessTemplate_ExpressionEvaluation_Works()
        {
            var person = new Person { Age = 5 };
            string input = "Age squared: @((Age * Age))";
            var expected = "Age squared: 25";

            var result = Eval.ProcessTemplate<Person, string>(_lexer, _parser, input, person);

            result.OnFailure(err => Assert.Fail($"Evaluation failed: {err.Message}"));
            result.OnSuccess(value => Assert.Equal(expected, value));
        }

        [Fact]
        public void ProcessTemplate_EscapedAt_IsPreserved()
        {
            var person = new Person { Name = "Bob" };
            string input = "Email: \\@bob or @@admin";
            var expected = "Email: @bob or @admin";

            var result = Eval.ProcessTemplate<Person, string>(_lexer, _parser, input, person);

            result.OnFailure(err => Assert.Fail($"Evaluation failed: {err.Message}"));
            result.OnSuccess(value => Assert.Equal(expected, value));
        }

        [Fact]
        public void ProcessTemplate_NullProperty_ReplacesWithEmpty()
        {
            var person = new Person { Name = null!, Age = 25 };
            string input = "Name: @Name";
            var expected = "Name: ";

            var result = Eval.ProcessTemplate<Person, string>(_lexer, _parser, input, person);

            result.OnFailure(err => Assert.Fail($"Evaluation failed: {err.Message}"));
            result.OnSuccess(value => Assert.Equal(expected, value));
        }

        /*
        [Fact]
        public void ProcessTemplate_ModelIsNull_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                Eval.ProcessTemplate(_lexer, _parser, "Test @Name", (Person)null!);
            });
        }
        */

        [Fact]
        public void ProcessTemplate_ModelWithNestedObject_DoesNotAccessDeepProperties()
        {
            var person = new Person { Name = "Eva", Address = new Address { City = "London", Zip = 12345 } };
            string input = "City: @Address.City";
            var expected = "City: London";

            var result = Eval.ProcessTemplate<Person, string>(_lexer, _parser, input, person);

            result.OnFailure(err => Assert.Fail($"Evaluation failed: {err.Message}"));
            result.OnSuccess(value => Assert.Equal(expected, value));
        }
    }
}
