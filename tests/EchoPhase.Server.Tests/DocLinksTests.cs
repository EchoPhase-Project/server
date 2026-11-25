// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.RegularExpressions;

namespace EchoPhase.Server.Tests
{
    /// <summary>
    /// The rules for persons and agents (CONTRIBUTING.md, AGENTS.md, README.md,
    /// docs/): a link in them to a file or a heading that is not there fails.
    /// Links to other sites are not followed.
    /// </summary>
    public partial class DocLinksTests
    {
        [Fact]
        public void Docs_AreFound()
        {
            var docs = Docs(RepositoryRoot()).ToList();

            Assert.Contains(docs, d => d.EndsWith("CONTRIBUTING.md", StringComparison.Ordinal));
            Assert.True(docs.Count(d => d.Contains(Path.Combine("docs", "contributing"), StringComparison.Ordinal)) > 5,
                "docs/contributing/ has fewer files than expected.");
        }

        [Fact]
        public void Links_GoToFilesAndHeadingsThatAreThere()
        {
            var root = RepositoryRoot();
            var broken = new List<string>();
            foreach (var doc in Docs(root))
            {
                var text = File.ReadAllText(doc);
                foreach (var link in LinksOf(text))
                {
                    var hash = link.IndexOf('#', StringComparison.Ordinal);
                    var path = hash < 0 ? link : link[..hash];
                    var anchor = hash < 0 ? null : link[(hash + 1)..];
                    var target = path.Length == 0
                        ? doc
                        : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(doc) ?? root, Uri.UnescapeDataString(path)));
                    var relative = Path.GetRelativePath(root, doc);
                    if (!File.Exists(target) && !Directory.Exists(target))
                        broken.Add($"{relative}: no file {link}");
                    else if (anchor is not null && target.EndsWith(".md", StringComparison.Ordinal)
                        && !AnchorsOf(File.ReadAllText(target)).Contains(anchor))
                        broken.Add($"{relative}: no heading #{anchor} in {(path.Length == 0 ? "this file" : path)}");
                }
            }

            Assert.True(broken.Count == 0, $"{broken.Count} broken link(s):\n" + string.Join("\n", broken));
        }

        [Theory]
        [InlineData("The API contract", "the-api-contract")]
        [InlineData("`Projector` and [Expose]", "projector-and-expose")]
        [InlineData("Commits and pull requests", "commits-and-pull-requests")]
        public void Slug_IsTheAnchorGitHubGives(string heading, string anchor)
            => Assert.Equal(anchor, Slug(heading));

        /// <summary>The anchor GitHub gives a heading: lower case, spaces to dashes.</summary>
        public static string Slug(string heading)
        {
            var text = heading.Trim().ToLowerInvariant().Replace("`", "").Replace("*", "").Replace("_", "");
            return NotInSlug().Replace(text, "").Replace(' ', '-');
        }

        private static HashSet<string> AnchorsOf(string text)
        {
            var anchors = new HashSet<string>(StringComparer.Ordinal);
            var fenced = false;
            foreach (var line in text.Split('\n'))
            {
                if (line.StartsWith("```", StringComparison.Ordinal))
                    fenced = !fenced;
                if (fenced)
                    continue;
                var heading = Heading().Match(line.TrimEnd('\r'));
                if (heading.Success)
                    anchors.Add(Slug(heading.Groups[1].Value));
            }
            return anchors;
        }

        /// <summary>The [text](target) links outside code, but not to other sites.</summary>
        private static IEnumerable<string> LinksOf(string text)
        {
            var prose = InlineCode().Replace(Fence().Replace(text, ""), "");
            return Link().Matches(prose)
                .Select(m => m.Groups[1].Value)
                .Where(target => !Scheme().IsMatch(target));
        }

        private static IEnumerable<string> Docs(string root)
        {
            foreach (var name in new[] { "CONTRIBUTING.md", "AGENTS.md", "README.md" })
            {
                var file = Path.Combine(root, name);
                if (File.Exists(file))
                    yield return file;
            }
            var docs = Path.Combine(root, "docs");
            if (Directory.Exists(docs))
                foreach (var file in Directory.EnumerateFiles(docs, "*.md", SearchOption.AllDirectories))
                    yield return file;
        }

        private static string RepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EchoPhase.slnx")))
                directory = directory.Parent;
            return directory?.FullName ?? throw new InvalidOperationException("EchoPhase.slnx not found above the test binaries.");
        }

        [GeneratedRegex(@"[^\p{L}\p{N}\- ]")]
        private static partial Regex NotInSlug();

        [GeneratedRegex(@"^#{1,6} (.+)$")]
        private static partial Regex Heading();

        [GeneratedRegex(@"```[\s\S]*?```")]
        private static partial Regex Fence();

        [GeneratedRegex(@"`[^`\n]*`")]
        private static partial Regex InlineCode();

        [GeneratedRegex(@"\]\(([^)\s]+)\)")]
        private static partial Regex Link();

        [GeneratedRegex(@"^[a-z][a-z\d+.-]*:", RegexOptions.IgnoreCase)]
        private static partial Regex Scheme();
    }
}
