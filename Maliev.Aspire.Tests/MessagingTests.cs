using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using System.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using Aspire.Hosting;
using Aspire.Hosting.Testing;

namespace Maliev.Aspire.Tests;

public class MessagingTests : IAsyncLifetime
{
    private DistributedApplicationFactory? _appFactory;
    private readonly ITestOutputHelper _output;

    public MessagingTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        var appHostAssembly = typeof(Projects.Maliev_Aspire_AppHost).Assembly;
        _appFactory = new DistributedApplicationFactory(appHostAssembly.EntryPoint!.DeclaringType!);
        await _appFactory.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_appFactory != null)
        {
            await _appFactory.DisposeAsync();
        }
    }

    [Fact]
    public async Task PaymentService_Publishes_Event_And_NotificationService_Receives_It()
    {
        // Arrange - Use test payment and order IDs for RabbitMQ integration test
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        _output.WriteLine("=== RabbitMQ Integration Test Starting ===");
        _output.WriteLine($"Order ID: {orderId}");
        _output.WriteLine($"Payment ID: {paymentId}");

        var paymentApiClient = _appFactory!.CreateHttpClient("maliev-paymentservice-api");

        // Step 1: Call PaymentService test endpoint to publish PaymentCompletedEvent to RabbitMQ
        var publishRequest = new
        {
            OrderId = orderId,
            PaymentId = paymentId,
            Amount = 1000.00,
            Currency = "THB"
        };

        _output.WriteLine("\n[Step 1] Calling PaymentService test endpoint to publish PaymentCompletedEvent to RabbitMQ...");
        var publishResponse = await paymentApiClient.PostAsJsonAsync("/payment/v1/test/publish-payment-completed", publishRequest);

        var publishContent = await publishResponse.Content.ReadAsStringAsync();
        _output.WriteLine($"Response: {publishResponse.StatusCode}");
        _output.WriteLine($"Content: {publishContent}");

        publishResponse.EnsureSuccessStatusCode();
        _output.WriteLine("✓ PaymentCompletedEvent published to RabbitMQ successfully");

        // Step 2: Wait for RabbitMQ message to be consumed by NotificationService
        _output.WriteLine("\n[Step 2] Waiting for RabbitMQ to deliver message to NotificationService...");
        await Task.Delay(TimeSpan.FromSeconds(5));

        // Step 3: Verify NotificationService received and processed the event
        _output.WriteLine("\n[Step 3] Verifying NotificationService received the event by querying delivery logs...");
        var notificationApiClient = _appFactory.CreateHttpClient("maliev-notificationservice-api");

        var deliveryLogsResponse = await notificationApiClient.GetAsync($"/notification/v1/delivery-logs?userId={orderId}&channelType=rabbitmq-event");

        var deliveryLogsContent = await deliveryLogsResponse.Content.ReadAsStringAsync();
        _output.WriteLine($"Delivery logs response: {deliveryLogsResponse.StatusCode}");
        _output.WriteLine($"Content: {deliveryLogsContent}");

        if (!deliveryLogsResponse.IsSuccessStatusCode)
        {
            _output.WriteLine("\n[Retry] Waiting additional 3 seconds and retrying query...");
            await Task.Delay(TimeSpan.FromSeconds(3));
            deliveryLogsResponse = await notificationApiClient.GetAsync($"/notification/v1/delivery-logs?userId={orderId}&channelType=rabbitmq-event");
            deliveryLogsContent = await deliveryLogsResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Retry response: {deliveryLogsResponse.StatusCode}");
            _output.WriteLine($"Content: {deliveryLogsContent}");
        }

        deliveryLogsResponse.EnsureSuccessStatusCode();

        // Step 4: Assert delivery log was created with correct details
        var deliveryLogs = await deliveryLogsResponse.Content.ReadFromJsonAsync<DeliveryLogsResponse>();
        Assert.NotNull(deliveryLogs);
        Assert.NotNull(deliveryLogs.Items);
        Assert.True(deliveryLogs.Items.Count > 0, "NotificationService should have received the PaymentCompletedEvent");

        var log = deliveryLogs.Items.First();
        Assert.Equal("rabbitmq-event", log.ChannelType);
        Assert.Equal("received", log.Status);
        Assert.Contains($"payment-{paymentId}", log.RecipientIdentifier);

        _output.WriteLine($"\n✓ Delivery log created: ID={log.Id}");
        _output.WriteLine($"✓ Channel type: {log.ChannelType}");
        _output.WriteLine($"✓ Status: {log.Status}");
        _output.WriteLine($"✓ Recipient: {log.RecipientIdentifier}");
        _output.WriteLine($"\n=== RabbitMQ Integration Test PASSED ===");
        _output.WriteLine($"✓ PaymentService successfully published PaymentCompletedEvent to RabbitMQ");
        _output.WriteLine($"✓ NotificationService successfully consumed the event and created delivery log");
        _output.WriteLine($"✓ End-to-end RabbitMQ messaging verified!");
    }

    private class PaymentResponse
    {
        public Guid TransactionId { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string CustomerId { get; set; } = string.Empty;
        public string OrderId { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    private class DeliveryLogsResponse
    {
        public List<DeliveryLogItem> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }

    private class DeliveryLogItem
    {
        public string Id { get; set; } = string.Empty;
        public string EventId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string ChannelType { get; set; } = string.Empty;
        public string RecipientIdentifier { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? MessageContent { get; set; }
    }
}
