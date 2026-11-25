// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Reflection;
using System.Text.Json;
using EchoPhase.Server.Controllers.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace EchoPhase.Server.Tests
{
    /// <summary>
    /// Every action declares its success response: a typed body
    /// (<c>[ProducesResponseType&lt;XResponse&gt;]</c> or <c>ActionResult&lt;XResponse&gt;</c>),
    /// 204 with none, or a redirect. A body without a type is not in the contract,
    /// so the web client cannot get its type and <c>openapi:check</c> cannot see it
    /// change. Every controller derives from <see cref="ApiControllerBase"/>.
    /// </summary>
    public class ResponseTypeTests
    {
        /// <summary>Controllers that are not part of the JSON API: the WebSocket upgrade.</summary>
        private static readonly HashSet<string> NotApi = new(StringComparer.Ordinal) { "GatewayController" };

        /// <summary>
        /// Success responses with no type, by action: work that goes on after a 202
        /// with no body, and the webhook's 200, which is the program's own answer.
        /// </summary>
        private static readonly HashSet<(string Action, int Status)> Untyped =
        [
            ("BlockRunsController.Cancel", StatusCodes.Status202Accepted),
            ("WebhooksController.Trigger", StatusCodes.Status200OK),
        ];

        /// <summary>Types that say nothing about the shape of a body.</summary>
        private static readonly HashSet<Type> Shapeless = [typeof(object), typeof(IActionResult), typeof(JsonElement)];

        [Fact]
        public void EveryController_DerivesFromApiControllerBase()
        {
            var others = Controllers()
                .Where(t => !typeof(ApiControllerBase).IsAssignableFrom(t) && !NotApi.Contains(t.Name))
                .Select(t => t.Name)
                .Order(StringComparer.Ordinal)
                .ToList();
            Assert.True(others.Count == 0, "Derive these from ApiControllerBase:\n" + string.Join("\n", others));
        }

        [Fact]
        public void EveryAction_DeclaresATypedSuccessResponse()
        {
            var untyped = Controllers()
                .Where(t => !NotApi.Contains(t.Name))
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any()))
                .Where(m => !HasTypedSuccess(m))
                .Select(Name)
                .Order(StringComparer.Ordinal)
                .ToList();
            Assert.True(untyped.Count == 0,
                $"{untyped.Count} action(s) without a typed success response: give each a response record and "
                + "[ProducesResponseType<XResponse>(...)], or 204 if it has no body:\n" + string.Join("\n", untyped));
        }

        [Fact]
        public void EveryUntypedEntry_StillNamesAnUntypedResponse()
        {
            var declared = Controllers()
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                .SelectMany(m => m.GetCustomAttributes<ProducesResponseTypeAttribute>()
                    .Where(a => !IsShaped(a.Type))
                    .Select(a => (Name(m), a.StatusCode)))
                .ToHashSet();
            var stale = Untyped.Where(u => !declared.Contains(u)).Select(u => $"{u.Action} {u.Status}").ToList();
            Assert.True(stale.Count == 0, "Remove these from Untyped (typed now, or gone):\n" + string.Join("\n", stale));
        }

        /// <summary>
        /// Each declared success status (2xx, 3xx) is 204, a redirect, an allowed untyped
        /// one, or has a type: on the attribute, or from an <c>ActionResult&lt;T&gt;</c>
        /// return, which also stands for an undeclared 200.
        /// </summary>
        private static bool HasTypedSuccess(MethodInfo method)
        {
            var success = method.GetCustomAttributes<ProducesResponseTypeAttribute>()
                .Where(a => a.StatusCode is >= 200 and < 400)
                .ToList();
            Type? returned = ActionResultType(method.ReturnType);
            bool typedReturn = returned is not null && !Shapeless.Contains(returned);
            if (success.Count == 0)
                return typedReturn;
            return success.All(a =>
                a.StatusCode is StatusCodes.Status204NoContent or >= 300
                || Untyped.Contains((Name(method), a.StatusCode))
                || IsShaped(a.Type)
                // ApiExplorer takes the type from ActionResult<T> for 200 and 201 only.
                || (typedReturn && a.Type == typeof(void) && a.StatusCode is 200 or 201));
        }

        private static bool IsShaped(Type? type) => type is not null && type != typeof(void) && !Shapeless.Contains(type);

        /// <summary>T of an action's <c>ActionResult&lt;T&gt;</c> or <c>Task&lt;ActionResult&lt;T&gt;&gt;</c>.</summary>
        private static Type? ActionResultType(Type type)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
                type = type.GetGenericArguments()[0];
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ActionResult<>)
                ? type.GetGenericArguments()[0]
                : null;
        }

        private static string Name(MethodInfo method) => $"{method.DeclaringType?.Name}.{method.Name}";

        private static IEnumerable<Type> Controllers()
            => typeof(ApiControllerBase).Assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t));
    }
}
