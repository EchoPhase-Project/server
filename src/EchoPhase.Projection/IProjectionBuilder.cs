namespace EchoPhase.Projection
{
    /// <summary>
    /// Non-generic interface so Projector can call Build() without knowing TItem.
    /// </summary>
    internal interface IProjectionBuilder
    {
        object? Build();
    }
}
