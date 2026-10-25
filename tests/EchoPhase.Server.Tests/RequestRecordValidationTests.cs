// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using EchoPhase.Server.Controllers.Api;

namespace EchoPhase.Server.Tests
{
    /// <summary>
    /// MVC refuses (500 on every request) a record whose validation
    /// attributes sit on the properties of its primary constructor
    /// (<c>[property: Range(...)]</c>): they belong on the parameters.
    /// </summary>
    public class RequestRecordValidationTests
    {
        [Fact]
        public void ValidationAttributes_AreOnConstructorParameters()
        {
            var offenders =
                from type in typeof(ApiControllerBase).Assembly.GetTypes()
                where type.GetMethod("<Clone>$") is not null
                from ctor in type.GetConstructors()
                from parameter in ctor.GetParameters()
                let property = parameter.Name is { } name ? type.GetProperty(name) : null
                where property is not null && property.GetCustomAttributes<ValidationAttribute>().Any()
                select $"{type.Name}.{property.Name}";

            Assert.Empty(offenders.Distinct());
        }
    }
}
