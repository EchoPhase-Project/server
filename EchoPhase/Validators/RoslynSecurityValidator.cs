using EchoPhase.Analysers;
using EchoPhase.Interfaces;
using EchoPhase.Settings;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Options;

namespace EchoPhase.Validators
{
    public class RoslynSecurityValidator : IRoslynSecurityValidator
    {
        private readonly ISet<string> _allowedAssemblies;
        private readonly ISet<string> _allowedTypes;

        public RoslynSecurityValidator(
            IOptions<RunnersSettings> options
        )
        {
            RunnersSettings settings = options.Value;

            _allowedTypes = settings.Roslyn.Allow ?? new HashSet<string>
            {
                "System.Math", "System.Text.Json.JsonSerializer"
            };

            _allowedAssemblies = _allowedTypes
                .Select(typeName => Type.GetType(typeName)?.Assembly?.GetName().Name)
                .Where(name => name != null)
                .Select(name => name!)
                .ToHashSet();
        }

        public IEnumerable<string> Validate(string code)
        {
            var diagnostics = new List<string>();

            var tree = CSharpSyntaxTree.ParseText(code);
            var root = tree.GetRoot();

            if (root.DescendantNodes().OfType<UsingDirectiveSyntax>().Any())
                diagnostics.Add("Using directives are not allowed.");

            var compilation = CSharpCompilation.Create("Validation")
                .AddReferences(AppDomain.CurrentDomain
                    .GetAssemblies()
                    .Where(a => _allowedAssemblies.Contains(a.GetName().Name!))
                    .Select(a => MetadataReference.CreateFromFile(a.Location)))
                .AddSyntaxTrees(tree);

            var semanticModel = compilation.GetSemanticModel(tree, ignoreAccessibility: true);
            var analyzer = new AllowAssemblyAnalyzer(semanticModel, _allowedAssemblies, _allowedTypes);
            analyzer.Visit(root);
            diagnostics.AddRange(analyzer.Violations);

            return diagnostics;
        }
    }
}
