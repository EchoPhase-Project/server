// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json;
using EchoPhase.Types.Result;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace EchoPhase.Server.Controllers.Api.v1.Common
{
    /// <summary>
    /// The API's error body: a stable code to branch on, a readable message and,
    /// when the error is about one input, that input's JSON name (camelCase) so a
    /// form can show it next to the field. No field: the error is about the request.
    /// </summary>
    public sealed record ErrorBody(string Code, string Message, string? Field = null)
    {
        public const string InvalidCode = "Invalid";

        public static ErrorBody From(IServiceError error) => new(error.Code, error.Message);

        public static ErrorBody From(IdentityError error) => new(error.Code, error.Description, FieldOf(error.Code));

        /// <summary>
        /// Model validation and JSON binding errors, one per message. Used for
        /// every 400 that [ApiController] produces, so all bad requests share
        /// this shape instead of ProblemDetails.
        /// </summary>
        /// <param name="modelState">The binding and validation errors of the request.</param>
        /// <param name="parameters">
        /// The action's parameter names: a key equal to one ("dto" for a missing
        /// body) is about the whole request, not an input.
        /// </param>
        public static IReadOnlyList<ErrorBody> From(ModelStateDictionary modelState, IEnumerable<string> parameters)
        {
            var requestKeys = parameters.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var entries = modelState.Where(entry => entry.Value is { Errors.Count: > 0 }).ToList();

            // A body that fails to deserialize also gets "The dto field is
            // required." under the parameter's key: noise next to the error
            // that says which field was wrong. Keep it only when it is alone.
            if (entries.Any(entry => !requestKeys.Contains(entry.Key)))
                entries.RemoveAll(entry => requestKeys.Contains(entry.Key));

            return entries
                .SelectMany(entry => entry.Value!.Errors.Select(error => new ErrorBody(
                    InvalidCode,
                    string.IsNullOrEmpty(error.ErrorMessage) ? "The value is not valid." : error.ErrorMessage,
                    requestKeys.Contains(entry.Key) ? null : FieldName(entry.Key))))
                .ToList();
        }

        // Identity reports problems by code only; these are the ones tied to an input.
        private static string? FieldOf(string code) => code switch
        {
            "DuplicateUserName" or "InvalidUserName" => "username",
            "DuplicateEmail" or "InvalidEmail" => "email",
            "InvalidTimeZone" => "timeZone",
            "InvalidName" => "name",
            _ when code.StartsWith("Password", StringComparison.Ordinal) => "password",
            _ => null
        };

        // "Username" or "$.username" -> "username"; "" or "$" -> no field.
        private static string? FieldName(string key)
        {
            var name = key.StartsWith("$.", StringComparison.Ordinal) ? key[2..] : key;
            return name.Length == 0 || name == "$" ? null : JsonNamingPolicy.CamelCase.ConvertName(name);
        }
    }
}
