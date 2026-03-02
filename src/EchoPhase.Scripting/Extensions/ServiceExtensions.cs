using EchoPhase.Scripting.Lexers;
using EchoPhase.Scripting.Parsers;
using EchoPhase.Scripting.Tokens;
using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.Scripting.Extensions
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddScripting(this IServiceCollection services)
        {
            services.AddScoped<ILexer<Token>, Lexer>();
            services.AddScoped<ILexer<TemplateToken>, TemplateLexer>();
            services.AddScoped<IParser<Token>, Parser>();
            services.AddScoped<IParser<TemplateToken>, TemplateParser>();
            services.AddScoped<ILexer<PathToken>, PathLexer>();
            services.AddScoped<IPathParser<PathToken>, PathParser>();

            return services;
        }
    }
}
