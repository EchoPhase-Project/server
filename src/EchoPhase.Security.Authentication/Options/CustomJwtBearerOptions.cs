using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace EchoPhase.Security.Authentication.Options
{
    public class CustomJwtBearerOptions : JwtBearerOptions
    {
        public new JwtBearerEvents Events { get; set; } = new();
    }
}
