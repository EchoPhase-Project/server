using EchoPhase.Clients.Discord;
using EchoPhase.Configuration.Settings;
using EchoPhase.Runners.Roslyn.Contexts;
using EchoPhase.Runners.Roslyn.Validators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EchoPhase.Runners.Roslyn
{
    public class RoslynRunner
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ISecurityValidator _validator;
        private readonly RunnersSettings _settings;

        private readonly ISet<string> _imports;

        public RoslynRunner(
            IServiceProvider serviceProvider,
            IOptions<RunnersSettings> settings,
            ISecurityValidator validator
        )
        {
            _serviceProvider = serviceProvider;
            _settings = settings.Value;
            _validator = validator;
            _imports = _settings.Roslyn.Import ?? new HashSet<string>
            {
                "System", "System.Text", "System.Text.Json"
            };
        }

        public async Task RunAsync<TI, T>(string code, TI payload) where T : TI
        {
            var diagnostics = _validator.Validate(code).ToArray();
            if (diagnostics.Length > 0)
                throw new InvalidOperationException("Script validation failed:\n" + string.Join("\n", diagnostics));

            var context = new ScriptContext
            {
                DiscordClient = GetService<IDiscordClient>()
            };
            var globals = new ScriptGlobals<TI> { Payload = payload, Context = context };

            var references = AppDomain.CurrentDomain
                .GetAssemblies()
                .Where(a => a.IsDynamic == false)
                .Select(a => MetadataReference.CreateFromFile(a.Location));

            var options = ScriptOptions.Default
                .WithReferences(references)
                .WithImports(_imports);

            var clientScript = CSharpScript.Create(code, options, typeof(IScriptGlobals<TI>));
            await clientScript.RunAsync(globals);
        }

        private T GetService<T>() where T : notnull =>
            _serviceProvider.GetRequiredService<T>();
    }
}
