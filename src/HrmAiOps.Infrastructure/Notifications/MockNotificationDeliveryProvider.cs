using HrmAiOps.Application.Abstractions;
using HrmAiOps.Domain.Core;

namespace HrmAiOps.Infrastructure.Notifications;

public sealed class MockNotificationDeliveryProvider : INotificationDeliveryProvider
{
    public NotificationDeliveryResult Deliver(NotificationDeliveryRequest request)
    {
        var provider = request.Channel == NotificationChannel.Email
            ? "MockEmailProvider"
            : "InAppProvider";

        return new NotificationDeliveryResult(
            provider,
            NotificationDeliveryStatus.Delivered,
            null,
            DateTimeOffset.UtcNow);
    }
}
