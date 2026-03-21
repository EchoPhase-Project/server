// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.QRCodes.Generators;
using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.QRCodes.Extensions
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddQRCodes(this IServiceCollection services)
        {
            services.AddKeyedSingleton<IQRCodeGenerator, PngQRCodeGenerator>(PngQRCodeGenerator.Format);
            services.AddKeyedSingleton<IQRCodeGenerator, SvgQRCodeGenerator>(SvgQRCodeGenerator.Format);
            services.AddSingleton<QRCodeService>();

            return services;
        }
    }
}
