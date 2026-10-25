// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.Notifications.Extensions
{
    public static class ServiceExtensions
    {
        /// <summary>
        /// Users' notifications (<see cref="NotificationService"/>) kept in
        /// ScyllaDB. The host adds how they are delivered live: an
        /// <see cref="INotificationPublisher"/>.
        /// </summary>
        public static IServiceCollection AddNotifications(this IServiceCollection services)
        {
            services.AddScoped<INotificationStore, ScyllaNotificationStore>();
            services.AddScoped<NotificationService>();
            return services;
        }
    }
}
