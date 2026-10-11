// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;
using EchoPhase.DAL.Abstractions.Repository;
using EchoPhase.DAL.Postgres.Repositories;
using EchoPhase.Security.Authentication.Jwt.Providers;
using Microsoft.AspNetCore.Identity;
using UserModel = EchoPhase.DAL.Postgres.Models.User;

namespace EchoPhase.Cli.Commands.User.Sessions
{
    public class SessionsSettings : UserSettings
    {
        [CommandOption("--revoke-all")]
        [Description("End every session of the user (and invalidate their access tokens)")]
        public bool RevokeAll { get; set; }
    }

    /// <summary>The user's sessions, or with --revoke-all, the end of all of them.</summary>
    public class SessionsCommand : AsyncCommand<SessionsSettings>
    {
        private readonly UserRepository _users;
        private readonly ISessionProvider _sessions;
        private readonly UserManager<UserModel> _userManager;

        public SessionsCommand(UserRepository users, ISessionProvider sessions, UserManager<UserModel> userManager)
        {
            _users = users;
            _sessions = sessions;
            _userManager = userManager;
        }

        public override async Task<int> ExecuteAsync(CommandContext context, SessionsSettings settings, CancellationToken cancellationToken)
        {
            if (await UserLookup.FindAsync(_users, settings.Username, cancellationToken) is not { } user)
                return CliExit.Failure;

            if (settings.RevokeAll)
            {
                await _sessions.RevokeAllAsync(user.Id, cancellationToken);
                await _userManager.UpdateSecurityStampAsync(user);
                if (settings.Verbose)
                    AnsiConsole.MarkupLineInterpolated($"[green]Every session of '{settings.Username}' is ended.[/]");
                return settings.Continue ? CliExit.StartServer : CliExit.Success;
            }

            var table = new Table().AddColumns("Id", "Device", "Created (UTC)", "Last refresh (UTC)");
            var cursor = new CursorOptions { Limit = 100 };
            while (true)
            {
                var page = await _sessions.GetSessionsAsync(user.Id, cursor, cancellationToken);
                foreach (var session in page.Data)
                    table.AddRow(session.Id.ToString(), Markup.Escape(session.DeviceId ?? ""),
                        session.CreatedAt.ToString("u"), session.UpdatedAt.ToString("u"));
                if (!page.HasMore)
                    break;
                cursor.After = page.NextCursor;
            }
            AnsiConsole.Write(table);
            return CliExit.Success;
        }
    }
}
