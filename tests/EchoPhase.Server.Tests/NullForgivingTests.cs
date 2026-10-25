// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace EchoPhase.Server.Tests
{
    /// <summary>
    /// The null-forgiving operator (<c>x!</c>, <c>null!</c>, <c>default!</c>) is
    /// not used anywhere: a value that may be null is checked, with an error
    /// that says what was missing. EF migrations, generated, are exempt.
    /// </summary>
    public class NullForgivingTests
    {
        [Fact]
        public void NoCode_SuppressesANullableWarning()
        {
            var root = RepositoryRoot();
            var offenders = new List<string>();
            foreach (var directory in new[] { "src", "tests" })
            {
                foreach (var file in Directory.EnumerateFiles(Path.Combine(root, directory), "*.cs", SearchOption.AllDirectories))
                {
                    var relative = Path.GetRelativePath(root, file);
                    if (relative.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                        || relative.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        || relative.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}"))
                        continue;

                    var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file));
                    foreach (var node in tree.GetRoot().DescendantNodes())
                    {
                        if (node.IsKind(SyntaxKind.SuppressNullableWarningExpression))
                        {
                            var line = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                            offenders.Add($"{relative}:{line}: {node}");
                        }
                    }
                }
            }

            Assert.True(offenders.Count == 0,
                $"{offenders.Count} null-forgiving operator(s); check for null instead:\n" + string.Join("\n", offenders));
        }

        private static string RepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EchoPhase.slnx")))
                directory = directory.Parent;
            return directory?.FullName ?? throw new InvalidOperationException("EchoPhase.slnx not found above the test binaries.");
        }
    }
}
