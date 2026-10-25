// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EchoPhase.Storage.Extensions
{
    public static class ServiceExtensions
    {
        /// <summary>
        /// <see cref="IObjectStorage"/> on the S3 of <c>Storage:S3</c>. Without
        /// one configured, it reports <see cref="IObjectStorage.IsConfigured"/>
        /// false and its operations throw <see cref="ObjectStorageNotConfiguredException"/>.
        /// </summary>
        public static IServiceCollection AddObjectStorage(this IServiceCollection services)
        {
            services.TryAddSingleton<IObjectStorage, S3ObjectStorage>();
            return services;
        }
    }
}
