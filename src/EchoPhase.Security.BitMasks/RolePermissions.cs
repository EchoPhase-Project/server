// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Security.BitMasks.Constants;

namespace EchoPhase.Security.BitMasks
{
    /// <summary>
    /// Roles are fixed, named bundles of permissions. A user's ceiling is the
    /// union of <see cref="Everyone"/> and the bundles of their roles; an
    /// interactive session gets the whole ceiling, an API client only the part
    /// of it that it requested.
    /// </summary>
    public static class RolePermissions
    {
        private static readonly string[] Crud = [Permissions.Read, Permissions.Create, Permissions.Update, Permissions.Delete];

        /// <summary>Granted to every authenticated user, with or without roles.</summary>
        public static readonly IReadOnlyDictionary<string, string[]> Everyone = new Dictionary<string, string[]>
        {
            [Resources.Users] = [Permissions.Read],
            [Resources.Sessions] = [Permissions.Read, Permissions.Delete],
            [Resources.ApiClients] = Crud,
            [Resources.Connections] = [Permissions.Read, Permissions.Create, Permissions.Delete],
            [Resources.OAuthApps] = [Permissions.Read, Permissions.Create, Permissions.Delete],
            [Resources.Scripts] = [Permissions.Read, Permissions.Execute],
            [Resources.Workflows] = [.. Crud, Permissions.Execute],
            [Resources.Files] = [Permissions.Read, Permissions.Create, Permissions.Delete],
            [Resources.Discord] = [Permissions.Read, Permissions.Connect],
            [Resources.Twitch] = [Permissions.Read],
            // The realtime gateway is how the webapp gets events; what each
            // socket receives is still bounded by its intents' permissions.
            [Resources.Gateway] = [Permissions.Connect],
        };

        /// <summary>
        /// What each role adds on top of <see cref="Everyone"/>. Higher roles
        /// include lower ones, so a staff member also has everything a dev has.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string[]>> ByRole =
            BuildRoleBundles();

        private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string[]>> BuildRoleBundles()
        {
            // Nothing beyond Everyone for now (the gateway used to be dev-only).
            var dev = new Dictionary<string, string[]>();

            var staff = Union(dev, new Dictionary<string, string[]>
            {
                [Resources.Roles] = [Permissions.Read],
            });

            var admin = Union(staff, new Dictionary<string, string[]>
            {
                [Resources.Roles] = [Permissions.Assign],
                [Resources.Users] = [Permissions.Impersonate, Permissions.Grant],
                [Resources.Maintenance] = [Permissions.Update],
            });

            return new Dictionary<string, IReadOnlyDictionary<string, string[]>>(StringComparer.Ordinal)
            {
                [Roles.Dev] = dev,
                [Roles.Staff] = staff,
                [Roles.Admin] = admin,
            };
        }

        public static bool IsKnownRole(string role) => ByRole.ContainsKey(role);

        /// <summary>
        /// Permissions that only come with a role, never as a per-user grant:
        /// the most sensitive access stays visible by looking at roles alone.
        /// </summary>
        public static readonly IReadOnlySet<string> RoleOnly = new HashSet<string>(StringComparer.Ordinal)
        {
            $"{Resources.Users}:{Permissions.Impersonate}",
            $"{Resources.Users}:{Permissions.Grant}",
            $"{Resources.Roles}:{Permissions.Assign}",
            $"{Resources.Maintenance}:{Permissions.Update}",
        };

        public static bool IsGrantable(string resource, string action)
            => !RoleOnly.Contains($"{resource}:{action}");

        /// <summary>
        /// Requested entries that are not a known resource and action, as
        /// <c>resource:action</c>.
        /// </summary>
        public static IReadOnlyList<string> Unknown(IReadOnlyDictionary<string, string[]> requested)
        {
            var resources = Resources.AsEnumerable().ToHashSet(StringComparer.Ordinal);
            var actions = Permissions.AsEnumerable().ToHashSet(StringComparer.Ordinal);

            return requested
                .SelectMany(kv => kv.Value.Select(action => (Resource: kv.Key, Action: action)))
                .Where(p => !resources.Contains(p.Resource) || !actions.Contains(p.Action))
                .Select(p => $"{p.Resource}:{p.Action}")
                .ToList();
        }

        // Lowest to highest; every role includes the ones before it.
        private static readonly string[] Ranking = [Roles.Dev, Roles.Staff, Roles.Admin];

        /// <summary>
        /// Position of the highest role held: 0 for none, 1 for dev, and so on.
        /// Used to stop impersonating someone of equal or higher standing.
        /// </summary>
        public static int Rank(IEnumerable<string> roles)
            => roles.Select(r => Array.IndexOf(Ranking, r) + 1).DefaultIfEmpty(0).Max();

        /// <summary>
        /// Everything a user with these roles may do. Unknown role names are
        /// ignored rather than failing, so a stray role in the database cannot
        /// lock its holder out.
        /// </summary>
        public static IReadOnlyDictionary<string, string[]> CeilingFor(IEnumerable<string> roles)
        {
            IReadOnlyDictionary<string, string[]> ceiling = Everyone;

            foreach (var role in roles)
                if (ByRole.TryGetValue(role, out var bundle))
                    ceiling = Union(ceiling, bundle);

            return ceiling;
        }

        /// <summary>
        /// <see cref="CeilingFor(IEnumerable{string})"/> plus per-user grants.
        /// Role-only entries in a grant are ignored, so a stray row cannot
        /// hand out impersonation or role assignment.
        /// </summary>
        public static IReadOnlyDictionary<string, string[]> CeilingFor(
            IEnumerable<string> roles,
            IEnumerable<KeyValuePair<string, string[]>> grants)
        {
            var granted = new Dictionary<string, string[]>(StringComparer.Ordinal);
            foreach (var (resource, actions) in grants)
            {
                var allowed = actions.Where(a => IsGrantable(resource, a)).ToArray();
                if (allowed.Length > 0)
                    granted[resource] = granted.TryGetValue(resource, out var existing)
                        ? existing.Union(allowed, StringComparer.Ordinal).ToArray()
                        : allowed;
            }

            return Union(CeilingFor(roles), granted);
        }

        /// <summary>
        /// The part of <paramref name="requested"/> that <paramref name="ceiling"/> allows.
        /// </summary>
        public static IReadOnlyDictionary<string, string[]> Intersect(
            IReadOnlyDictionary<string, string[]> ceiling,
            IReadOnlyDictionary<string, string[]> requested)
        {
            var result = new Dictionary<string, string[]>(StringComparer.Ordinal);

            foreach (var (resource, actions) in requested)
            {
                if (!ceiling.TryGetValue(resource, out var allowed))
                    continue;

                var granted = actions.Intersect(allowed, StringComparer.Ordinal).ToArray();
                if (granted.Length > 0)
                    result[resource] = granted;
            }

            return result;
        }

        /// <summary>
        /// Requested entries the ceiling does not allow, as <c>resource:action</c>.
        /// </summary>
        public static IReadOnlyList<string> Exceeding(
            IReadOnlyDictionary<string, string[]> ceiling,
            IReadOnlyDictionary<string, string[]> requested)
        {
            var exceeding = new List<string>();

            foreach (var (resource, actions) in requested)
            {
                ceiling.TryGetValue(resource, out var allowed);
                foreach (var action in actions)
                    if (allowed is null || !allowed.Contains(action, StringComparer.Ordinal))
                        exceeding.Add($"{resource}:{action}");
            }

            return exceeding;
        }

        private static Dictionary<string, string[]> Union(
            IReadOnlyDictionary<string, string[]> left,
            IReadOnlyDictionary<string, string[]> right)
        {
            var result = left.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);

            foreach (var (resource, actions) in right)
                result[resource] = result.TryGetValue(resource, out var existing)
                    ? existing.Union(actions, StringComparer.Ordinal).ToArray()
                    : actions;

            return result;
        }
    }
}
