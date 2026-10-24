// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Reflection;
using System.Text.Json.Nodes;
using EchoPhase.Types.Attributes;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace EchoPhase.Server.OpenApi
{
    /// <summary>
    /// For an enum marked [ExtensibleEnum]: the known values as
    /// x-extensible-enum instead of enum. Contract checks (oasdiff) then treat
    /// a new value as compatible, while a removed one is still caught; client
    /// generators type the field as a string.
    /// </summary>
    public sealed class ExtensibleEnumSchemaFilter : ISchemaFilter
    {
        public const string Extension = "x-extensible-enum";

        public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
        {
            if (!context.Type.IsEnum || context.Type.GetCustomAttribute<ExtensibleEnumAttribute>() is null)
                return;
            if (schema is not OpenApiSchema writable || writable.Enum is not { Count: > 0 } values)
                return;

            var known = new JsonArray();
            foreach (var value in values)
                known.Add(value?.DeepClone());

            writable.Extensions ??= new Dictionary<string, IOpenApiExtension>();
            writable.Extensions[Extension] = new JsonNodeExtension(known);
            writable.Enum = null;
        }
    }
}
