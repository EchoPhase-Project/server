// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using Microsoft.Extensions.DependencyInjection;
using EchoPhase.QRCodes.Generators;

namespace EchoPhase.QRCodes
{
    public class QRCodeService
    {
        private readonly IServiceProvider _serviceProvider;

        public QRCodeService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IQRCodeGenerator GetGenerator(string format)
        {
            return _serviceProvider.GetKeyedService<IQRCodeGenerator>(format.ToLowerInvariant())
                ?? throw new InvalidOperationException($"No QR generator registered for format '{format}'.");
        }
    }
}
