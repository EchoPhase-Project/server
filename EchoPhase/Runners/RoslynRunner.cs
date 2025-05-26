using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

using EchoPhase.Interfaces;
using EchoPhase.Runners.Models;

namespace EchoPhase.Runners
{
	public class RoslynRunner
	{
		private readonly IServiceProvider _serviceProvider;
		public readonly ISet<string> bannedIdentifiers = new HashSet<string>()
			{ "System", "EchoPhase", "Microsoft", "DllImport", "Process", "File", "HttpClient", "Grpc" };
		public readonly ISet<string> imports = new HashSet<string>()
			{ "System.Math", "System.Text", "System.Text.Json", "System.Text.Json.Serialization", "System.Net.Mime" };

		public RoslynRunner(
			IServiceProvider serviceProvider
		)
		{
			_serviceProvider = serviceProvider;
		}

		public async Task RunAsync<TI, T>(string code, TI payload)
			where T : TI
		{
			IDiscordClient discordClient = GetService<IDiscordClient>();
			ScriptContext context = new ScriptContext()
			{
				DiscordClient = discordClient
			};

			ScriptGlobals<TI> globals = new ScriptGlobals<TI>() 
			{
				Payload = payload, 
				Context = context 
			};

			ValidateScript(code);

			var options = ScriptOptions.Default
				.WithReferences(
					typeof(object).Assembly,
					typeof(object).Assembly,
					typeof(IScriptGlobals<TI>).Assembly)
				.WithImports(imports);

			Script clientScript = CSharpScript.Create(code, options, typeof(IScriptGlobals<TI>));
			var scriptState = await clientScript.RunAsync(globals);
		}

		protected T GetService<T>() where T : notnull
		{
			return _serviceProvider.GetRequiredService<T>();
		}

		private void ValidateScript(string code)
		{
			var tree = CSharpSyntaxTree.ParseText(code);
			var root = tree.GetRoot();

			var usings = root.DescendantNodes().OfType<UsingDirectiveSyntax>();
			if (usings.Any())
				throw new InvalidOperationException("Using directives are not allowed.");

			if (bannedIdentifiers.Any(b => code.Contains(b)))
				throw new InvalidOperationException("Access to restricted API is denied.");
		}
	}
}
