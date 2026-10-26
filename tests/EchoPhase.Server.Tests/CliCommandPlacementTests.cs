// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Server.Controllers.Api;
using Spectre.Console.Cli;

namespace EchoPhase.Server.Tests
{
    /// <summary>
    /// Commands live in EchoPhase.Cli. What a command needs from the server
    /// is an interface there, implemented here (IWorkflowEditor).
    /// </summary>
    public class CliCommandPlacementTests
    {
        [Fact]
        public void Server_DefinesNoCommands()
        {
            var offenders =
                from type in typeof(ApiControllerBase).Assembly.GetTypes()
                where typeof(ICommand).IsAssignableFrom(type)
                select type.FullName;

            Assert.Empty(offenders);
        }
    }
}
