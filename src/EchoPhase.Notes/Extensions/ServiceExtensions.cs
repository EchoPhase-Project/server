// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.Notes.Extensions
{
    public static class ServiceExtensions
    {
        /// <summary>
        /// Users' notes (<see cref="NoteService"/>). The host adds what they
        /// tell users about: an <see cref="INoteEvents"/>.
        /// </summary>
        public static IServiceCollection AddNotes(this IServiceCollection services)
        {
            services.AddScoped<NoteService>();
            return services;
        }
    }
}
