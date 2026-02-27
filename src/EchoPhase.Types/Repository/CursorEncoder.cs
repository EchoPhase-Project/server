namespace EchoPhase.Types.Repository
{
    public static class CursorEncoder
    {
        public static string Encode(Guid id)
            => Convert.ToBase64String(id.ToByteArray());

        public static Guid? Decode(string? cursor)
        {
            if (cursor is null) return null;
            try
            {
                var bytes = Convert.FromBase64String(cursor);
                return new Guid(bytes);
            }
            catch
            {
                return null;
            }
        }
    }
}
