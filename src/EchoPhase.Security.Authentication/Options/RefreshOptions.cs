using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace EchoPhase.Security.Authentication.Options
{
    public class RefreshOptions : JwtBearerOptions
    {
        public new JwtBearerEvents Events { get; set; } = new();
    }
}
