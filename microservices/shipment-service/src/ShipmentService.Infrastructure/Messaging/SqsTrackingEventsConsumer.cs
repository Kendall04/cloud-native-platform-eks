using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShipmentService.Application.Interfaces;
using ShipmentService.Infrastructure.Configuration;

namespace ShipmentService.Infrastructure.Messaging;

public sealed class SqsTrackingEventsConsumer(
    IAmazonSQS sqs,
    IOptions<AwsOptions> awsOptions,
    IOptions<ShipmentConsumerOptions> shipmentConsumerOptions,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<SqsTrackingEventsConsumer> logger) : BackgroundService
{
    private readonly AwsOptions _awsOptions = awsOptions.Value;
    private readonly ShipmentConsumerOptions _consumerOptions = shipmentConsumerOptions.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Starting shipment SQS consumer for queue {QueueUrl} with maxMessagesPerPoll={MaxMessagesPerPoll} waitTimeSeconds={WaitTimeSeconds}",
            _awsOptions.ShipmentEventsQueueUrl,
            _consumerOptions.MaxMessagesPerPoll,
            _consumerOptions.WaitTimeSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            ReceiveMessageResponse response;

            try
            {
                response = await sqs.ReceiveMessageAsync(
                    new ReceiveMessageRequest
                    {
                        QueueUrl = _awsOptions.ShipmentEventsQueueUrl,
                        MaxNumberOfMessages = _consumerOptions.MaxMessagesPerPoll,
                        WaitTimeSeconds = _consumerOptions.WaitTimeSeconds,
                        AttributeNames = ["All"]
                    },
                    stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Failed to poll shipment events queue {QueueUrl}",
                    _awsOptions.ShipmentEventsQueueUrl);

                try
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(_consumerOptions.FailureBackoffSeconds),
                        stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                continue;
            }

            foreach (var message in response.Messages ?? [])
            {
                try
                {
                    logger.LogInformation(
                        "Consuming SQS message {MessageId} from queue {QueueUrl}",
                        message.MessageId,
                        _awsOptions.ShipmentEventsQueueUrl);

                    using var scope = serviceScopeFactory.CreateScope();
                    var processor = scope.ServiceProvider.GetRequiredService<ITrackingEventProcessor>();
                    var result = await processor.ProcessAsync(message.Body, stoppingToken);

                    logger.LogInformation(
                        "Processed SQS message {MessageId} with outcome {Outcome}",
                        message.MessageId,
                        result.Outcome);

                    await sqs.DeleteMessageAsync(
                        _awsOptions.ShipmentEventsQueueUrl,
                        message.ReceiptHandle,
                        stoppingToken);

                    logger.LogInformation(
                        "Deleted SQS message {MessageId} from queue {QueueUrl}",
                        message.MessageId,
                        _awsOptions.ShipmentEventsQueueUrl);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    logger.LogError(
                        exception,
                        "Failed to process SQS message {MessageId}. Message will remain available for retry.",
                        message.MessageId);
                }
            }
        }

        logger.LogInformation(
            "Stopping shipment SQS consumer for queue {QueueUrl}",
            _awsOptions.ShipmentEventsQueueUrl);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Received shutdown signal for shipment SQS consumer on queue {QueueUrl}",
            _awsOptions.ShipmentEventsQueueUrl);

        await base.StopAsync(cancellationToken);
    }
}
