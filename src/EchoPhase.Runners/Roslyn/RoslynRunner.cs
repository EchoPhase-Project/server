// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Clients.Discord;
using EchoPhase.Configuration.Runner;
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
        private readonly RunnerOptions _settings;
        private readonly ISet<string> _imports;

        public RoslynRunner(
            IServiceProvider serviceProvider,
            IOptions<RunnerOptions> settings,
            ISecurityValidator validator)
        {
            _serviceProvider = serviceProvider;
            _settings = settings.Value;
            _validator = validator;
            _imports = _settings.Roslyn.Import ?? new HashSet<string>
            {
                "System", "System.Text", "System.Text.Json"
            };
        }

        public async Task RunAsync<TPayload>(string code, TPayload payload)
        {
            var diagnostics = _validator.Validate(code).ToArray();
            if (diagnostics.Length > 0)
                throw new InvalidOperationException(
                    "Script validation failed:\n" + string.Join("\n", diagnostics));

            var context = new ScriptContext
            {
                DiscordClient = GetService<IDiscordClient>()
            };

            var globals = new ScriptGlobals<TPayload> { Payload = payload, Context = context };

            var references = AppDomain.CurrentDomain
                .GetAssemblies()
                .Where(a => !a.IsDynamic)
                .Select(a => MetadataReference.CreateFromFile(a.Location));

            var options = ScriptOptions.Default
                .WithReferences(references)
                .WithImports(_imports);

            // Globals type is ScriptGlobals<TPayload> — concrete, no interface wrapper.
            var script = CSharpScript.Create(code, options, typeof(ScriptGlobals<TPayload>));
            await script.RunAsync(globals);
        }

        private T GetService<T>() where T : notnull =>
            _serviceProvider.GetRequiredService<T>();
    }
}
