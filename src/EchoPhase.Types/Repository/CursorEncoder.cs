using System.Text;
using System.Text.Json;

namespace EchoPhase.Types.Repository
{
    public record CursorValue(Guid Id, DateTime CreatedAt);

    public static class CursorEncoder
    {
        public static string Encode(Guid id, DateTime createdAt)
        {
            var json = JsonSerializer.Serialize(new CursorValue(id, createdAt));
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        }

        public static CursorValue? Decode(string? cursor)
        {
            if (cursor is null) return null;
            try
            {
                var json = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
                return JsonSerializer.Deserialize<CursorValue>(json);
            }
            catch { return null; }
        }
    }
}
