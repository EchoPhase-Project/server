// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.QRCodes.Extensions
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddQRCodes(this IServiceCollection services)
        {
            services.AddSingleton<QRCodeService>();

            return services;
        }
    }
}
