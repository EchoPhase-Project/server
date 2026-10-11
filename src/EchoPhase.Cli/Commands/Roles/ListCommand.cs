// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Security.BitMasks;

namespace EchoPhase.Cli.Commands.Roles
{
    public class ListSettings : CommandSettings
    {
    }

    /// <summary>The roles defined in code and what each one grants, on top of everyone's.</summary>
    public class ListCommand : Command<ListSettings>
    {
        public override int Execute(CommandContext context, ListSettings settings, CancellationToken cancellationToken)
        {
            // Everyone's permissions first, then what each role adds to them.
            var table = new Table().AddColumns("Role", "Resource", "Actions");
            AddRows(table, "(everyone)", RolePermissions.Everyone);
            foreach (var (role, permissions) in RolePermissions.ByRole)
                AddRows(table, role, permissions);
            AnsiConsole.Write(table);
            return CliExit.Success;
        }

        private static void AddRows(Table table, string role, IReadOnlyDictionary<string, string[]> permissions)
        {
            if (permissions.Count == 0)
                table.AddRow(Markup.Escape(role), "[grey]nothing beyond everyone[/]", "");
            foreach (var (resource, actions) in permissions.OrderBy(p => p.Key, StringComparer.Ordinal))
                table.AddRow(Markup.Escape(role), Markup.Escape(resource), Markup.Escape(string.Join(", ", actions)));
        }
    }
}
