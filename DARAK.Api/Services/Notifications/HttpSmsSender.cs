using System.Net.Http.Json;
using DARAK.Api.Interfaces;
using Microsoft.Extensions.Options;

namespace DARAK.Api.Services.Notifications;

public sealed class HttpSmsSender(
    HttpClient httpClient,
    IOptions<NotificationOptions> options) : ISmsSender
{
    public async Task<NotificationDeliveryResult> SendAsync(
        SmsNotificationMessage message,
        CancellationToken cancellationToken = default)
    {
        var smsOptions = options.Value.Sms;
        if (!smsOptions.Enabled)
        {
            return NotificationDeliveryResult.Skipped(smsOptions.ProviderName, "SMS delivery is disabled.");
        }

        if (string.IsNullOrWhiteSpace(smsOptions.EndpointUrl)
            || string.IsNullOrWhiteSpace(smsOptions.ApiKey)
            || string.IsNullOrWhiteSpace(message.ToPhoneNumber))
        {
            return NotificationDeliveryResult.Skipped(
                smsOptions.ProviderName,
                "SMS delivery is not fully configured.");
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, smsOptions.EndpointUrl)
            {
                Content = JsonContent.Create(new
                {
                    to = message.ToPhoneNumber,
                    body = message.Body,
                    senderId = smsOptions.SenderId
                })
            };

            request.Headers.TryAddWithoutValidation("X-API-Key", smsOptions.ApiKey);

            using var response = await httpClient.SendAsync(request, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return NotificationDeliveryResult.Failed(
                    smsOptions.ProviderName,
                    $"SMS provider returned {(int)response.StatusCode}: {responseText}");
            }

            return NotificationDeliveryResult.Success(
                smsOptions.ProviderName,
                string.IsNullOrWhiteSpace(responseText) ? null : responseText);
        }
        catch (Exception exception)
        {
            return NotificationDeliveryResult.Failed(smsOptions.ProviderName, exception.Message);
        }
    }
}
