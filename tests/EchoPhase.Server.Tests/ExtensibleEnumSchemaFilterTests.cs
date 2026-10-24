// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using EchoPhase.Runners.Blocks;
using EchoPhase.Runners.Blocks.Contexts;
using EchoPhase.Server.OpenApi;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace EchoPhase.Server.Tests
{
    public class ExtensibleEnumSchemaFilterTests
    {
        private static OpenApiSchema SchemaOf(Type type)
        {
            var options = new SchemaGeneratorOptions();
            options.SchemaFilters.Add(new ExtensibleEnumSchemaFilter());
            var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
            var generator = new SchemaGenerator(options, new JsonSerializerDataContractResolver(json));
            var repository = new SchemaRepository();

            generator.GenerateSchema(type, repository);
            return Assert.IsType<OpenApiSchema>(repository.Schemas[type.Name]);
        }

        [Fact]
        public void AnExtensibleEnum_ListsItsValuesAsExtensible_NotAsAClosedEnum()
        {
            var schema = SchemaOf(typeof(BlockTypes));

            Assert.True(schema.Enum is null or { Count: 0 });
            Assert.NotNull(schema.Extensions);
            var extension = Assert.IsType<JsonNodeExtension>(schema.Extensions[ExtensibleEnumSchemaFilter.Extension]);
            var values = Assert.IsType<JsonArray>(extension.Node).Select(v => v?.GetValue<string>()).ToList();
            Assert.Equal(Enum.GetNames<BlockTypes>(), values);
        }

        [Fact]
        public void OtherEnums_StayClosed()
        {
            var schema = SchemaOf(typeof(WebhookRespondMode));

            Assert.NotNull(schema.Enum);
            Assert.Equal(Enum.GetNames<WebhookRespondMode>().Length, schema.Enum.Count);
            Assert.False(schema.Extensions?.ContainsKey(ExtensibleEnumSchemaFilter.Extension) ?? false);
        }
    }
}
