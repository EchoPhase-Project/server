// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Security.BitMasks.Tests
{
    public class RolePermissionsTests
    {
        private static KeyValuePair<string, string[]> Grant(string resource, params string[] actions) => new(resource, actions);

        [Fact]
        public void Grants_AddToTheRoleCeiling()
        {
            var ceiling = RolePermissions.CeilingFor([], [Grant(Resources.Roles, Permissions.Read)]);

            Assert.Contains(Permissions.Read, ceiling[Resources.Roles]);
            // Everyone's permissions are still there.
            Assert.Contains(Permissions.Execute, ceiling[Resources.Scripts]);
        }

        [Fact]
        public void Grants_MergeWithRolePermissionsOnTheSameResource()
        {
            var ceiling = RolePermissions.CeilingFor([], [Grant(Resources.Users, Permissions.Delete)]);

            Assert.Equal(
                [Permissions.Delete, Permissions.Read],
                ceiling[Resources.Users].Order(StringComparer.Ordinal));
        }

        [Fact]
        public void RoleOnlyActions_InAGrantAreIgnored()
        {
            var ceiling = RolePermissions.CeilingFor([], [
                Grant(Resources.Users, Permissions.Impersonate, Permissions.Grant),
                Grant(Resources.Roles, Permissions.Assign)
            ]);

            Assert.DoesNotContain(Permissions.Impersonate, ceiling[Resources.Users]);
            Assert.DoesNotContain(Permissions.Grant, ceiling[Resources.Users]);
            Assert.False(ceiling.ContainsKey(Resources.Roles));
        }

        [Fact]
        public void RoleOnlyActions_StillComeWithTheirRole()
        {
            var ceiling = RolePermissions.CeilingFor([Roles.Admin], []);

            Assert.Contains(Permissions.Impersonate, ceiling[Resources.Users]);
            Assert.Contains(Permissions.Grant, ceiling[Resources.Users]);
            Assert.Contains(Permissions.Assign, ceiling[Resources.Roles]);
        }

        [Theory]
        [InlineData("users", "impersonate", false)]
        [InlineData("users", "grant", false)]
        [InlineData("roles", "assign", false)]
        [InlineData("gateway", "connect", true)]
        [InlineData("roles", "read", true)]
        public void IsGrantable(string resource, string action, bool expected)
            => Assert.Equal(expected, RolePermissions.IsGrantable(resource, action));

        [Fact]
        public void Unknown_ReportsMadeUpResourcesAndActions()
        {
            var unknown = RolePermissions.Unknown(new Dictionary<string, string[]>
            {
                [Resources.Gateway] = [Permissions.Connect, "fly"],
                ["nowhere"] = [Permissions.Read]
            });

            Assert.Equal(["gateway:fly", "nowhere:read"], unknown.Order(StringComparer.Ordinal));
        }
    }
}
