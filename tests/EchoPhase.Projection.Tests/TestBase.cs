namespace EchoPhase.Projection.Tests
{
    public abstract class TestBase
    {
        protected static Dictionary<string, object?> AsDictionary(object? result) =>
            Assert.IsType<Dictionary<string, object?>>(result);

        protected static List<object?> AsList(object? result) =>
            Assert.IsType<List<object?>>(result);
    }
}
