// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Reflection;
using EchoPhase.Server.Controllers.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace EchoPhase.Server.Tests
{
    /// <summary>
    /// Every API action declares its success response: a typed body
    /// (<c>[ProducesResponseType&lt;XResponse&gt;]</c>), or 204 / 202 with none, or a
    /// redirect. A body
    /// without a type is not in the contract, so the web client cannot get its
    /// type and <c>openapi:check</c> cannot see it change.
    /// </summary>
    public class ResponseTypeTests
    {
        /// <summary>
        /// Actions from before the rule, without a typed success response.
        /// Empty since 2026-11-25; keep it that way.
        /// </summary>
        private static readonly HashSet<string> Untyped = new(StringComparer.Ordinal)
        {
        };

        /// <summary>
        /// Success responses whose body is not ours to type: the webhook's 200 is the
        /// program's own answer (its status, headers and body, any content).
        /// </summary>
        private static readonly (string Action, int Status)[] FreeFormResponses =
        [
            ("WebhooksController.Trigger", StatusCodes.Status200OK),
        ];

        [Fact]
        public void EveryAction_DeclaresATypedSuccessResponse()
        {
            var untyped = Actions()
                .Where(a => !HasTypedSuccess(a.Method))
                .Select(a => a.Name)
                .ToHashSet(StringComparer.Ordinal);

            var added = untyped.Except(Untyped).Order(StringComparer.Ordinal).ToList();
            Assert.True(added.Count == 0,
                $"{added.Count} action(s) without a typed success response: give each a response record "
                + "and [ProducesResponseType<XResponse>(...)], or 204 if it has no body:\n" + string.Join("\n", added));
        }

        [Fact]
        public void TheListOfOldActions_HasNoActionThatIsTypedOrGone()
        {
            var untyped = Actions()
                .Where(a => !HasTypedSuccess(a.Method))
                .Select(a => a.Name)
                .ToHashSet(StringComparer.Ordinal);

            var stale = Untyped.Except(untyped).Order(StringComparer.Ordinal).ToList();
            Assert.True(stale.Count == 0,
                "These actions are typed now (or gone): remove them from ResponseTypeTests.Untyped:\n"
                + string.Join("\n", stale));
        }

        /// <summary>
        /// Each declared success status is 204, 202 without a body, a redirect, or has a type: on the attribute, or
        /// from an <c>ActionResult&lt;T&gt;</c> return (the compiler checks it), which also
        /// stands for an undeclared 200.
        /// </summary>
        private static bool HasTypedSuccess(MethodInfo method)
        {
            var success = method.GetCustomAttributes<ProducesResponseTypeAttribute>()
                .Where(a => a.StatusCode is >= 200 and < 400)
                .ToList();
            bool typedReturn = ActionResultType(method.ReturnType) is not null;
            if (success.Count == 0)
                return typedReturn;
            return success.All(a =>
                a.StatusCode is StatusCodes.Status204NoContent or StatusCodes.Status202Accepted or >= 300
                || FreeFormResponses.Contains(($"{method.DeclaringType?.Name}.{method.Name}", a.StatusCode))
                || (a.Type is not null && a.Type != typeof(void) && a.Type != typeof(object))
                || typedReturn);
        }

        /// <summary>T of an action's <c>ActionResult&lt;T&gt;</c> or <c>Task&lt;ActionResult&lt;T&gt;&gt;</c>.</summary>
        private static Type? ActionResultType(Type type)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
                type = type.GetGenericArguments()[0];
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ActionResult<>)
                ? type.GetGenericArguments()[0]
                : null;
        }

        private static IEnumerable<(string Name, MethodInfo Method)> Actions()
            => typeof(ApiControllerBase).Assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && typeof(ApiControllerBase).IsAssignableFrom(t))
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any())
                    .Select(m => ($"{t.Name}.{m.Name}", m)));
    }
}
