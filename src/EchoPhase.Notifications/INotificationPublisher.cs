// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Scylla.Entities;

namespace EchoPhase.Notifications
{
    /// <summary>Delivers a notification live (the realtime gateway), on top of keeping it.</summary>
    public interface INotificationPublisher
    {
        Task PublishAsync(Notification notification, CancellationToken ct = default);
    }
}
