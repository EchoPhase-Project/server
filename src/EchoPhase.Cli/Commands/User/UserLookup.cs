// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Repositories;
using Microsoft.EntityFrameworkCore;
using UserModel = EchoPhase.DAL.Postgres.Models.User;

namespace EchoPhase.Cli.Commands.User
{
    internal static class UserLookup
    {
        /// <summary>The user with this user name, or null after saying so.</summary>
        public static async Task<UserModel?> FindAsync(UserRepository users, string username, CancellationToken ct)
        {
            var user = (await users.Query().WithUserNames(username).ToListAsync(ct)).FirstOrDefault();
            if (user is null)
                AnsiConsole.MarkupLineInterpolated($"[red]No user '{username}'.[/]");
            return user;
        }

        /// <summary>
        /// A secret from standard input when it is redirected (scripts, kubectl exec -i),
        /// otherwise asked for without echo.
        /// </summary>
        public static string ReadSecret(string prompt)
            => Console.IsInputRedirected
                ? (Console.In.ReadLine() ?? string.Empty).TrimEnd('\r', '\n')
                : AnsiConsole.Prompt(new TextPrompt<string>(prompt).Secret());
    }
}
