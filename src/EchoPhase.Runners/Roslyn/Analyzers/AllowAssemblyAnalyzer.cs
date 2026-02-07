using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EchoPhase.Runners.Roslyn.Analyzers
{
    public class AllowAssemblyAnalyzer : CSharpSyntaxWalker
    {
        private readonly SemanticModel _semanticModel;
        private readonly ISet<string> _allowedAssemblies;
        private readonly ISet<string> _allowedTypes;

        public List<string> Violations { get; } = new();

        public AllowAssemblyAnalyzer(SemanticModel semanticModel, ISet<string> allowedAssemblies, ISet<string> allowedTypes)
        {
            _semanticModel = semanticModel;
            _allowedAssemblies = allowedAssemblies;
            _allowedTypes = allowedTypes;
        }

        public override void VisitIdentifierName(IdentifierNameSyntax node)
        {
            CheckSymbol(node);
            base.VisitIdentifierName(node);
        }

        public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            CheckSymbol(node);
            base.VisitMemberAccessExpression(node);
        }

        private void CheckSymbol(SyntaxNode node)
        {
            var symbol = _semanticModel.GetSymbolInfo(node).Symbol;
            if (symbol == null) return;

            var asmName = symbol.ContainingAssembly?.Name;
            var typeName = symbol.ContainingType?.ToDisplayString();

            if (asmName != null && !_allowedAssemblies.Contains(asmName))
                Violations.Add($"Disallowed assembly: {asmName} ({symbol}) at {node.GetLocation().GetLineSpan().StartLinePosition}");

            if (typeName != null && !_allowedTypes.Contains(typeName))
                Violations.Add($"Disallowed type: {typeName} at {node.GetLocation().GetLineSpan().StartLinePosition}");
        }
    }
}
