# Handle IoT-Hub failover on DeviceClients

The IoTHub DeviceClients work pretty well and can automatically recover from failover __unless AMQP connection pooling is used__. If AMQP connection pooling is really required, the solution is to restart the device client. This can be done by using `deviceClient.SetConnectionStatusChangesHandler(...)` and recreating the DeviceClient in the handler on certain conditions.

There may be other criteria affecting the failover behaviour besides the one tackled by the ones mentioned here (i.e. network configuration such as DNS cache, etc.). 

## Without AMQP connection pooling

Without AMQP connection pooling enabled, the DeviceClient is able to reconnect after the failover. After a certain time during failover, the DeviceClient `SendEventAsync(...)` throws an exception each time it is called. This shows that the message was not properly sent and the message can be stored in an internal buffer like a queue to be sent after the failover. Once the failover is finished, the `SendEventAsync(...)` method will succeed and the client can continue.

## With AMQP connection pooling

We reported this special case for clarification to the [azure-iot-sdk-csharp](https://github.com/Azure/azure-iot-sdk-csharp) project, see: [Not reconnecting after IoT Hub failover when AmqpConnectionPoolSettings.Pooling is enabled #1312](https://github.com/Azure/azure-iot-sdk-csharp/issues/1312)

In the meantime we used the following workaround:  
To detect the failover and restart the `DeviceClient`, the `DeviceClient.SetConnectionStatusChangesHandler(...)` can be used. The `ConnectionStatusChanged(...)` handler method gets then new `ConnectionStatus` and the `ConnectionStatusChangeReason` when it is called, which allow determining if it makes sense to reinitialize the `DeviceClient` by trying to close the old `DeviceClient` and recreating a new `DeviceClient` instance under certain conditions.

There are some other options such as the RetryPolicy and the OperationTimeoutInMilliseconds which allow to configure how the DeviceClient reacts on errors. We were using the default values during our experiments.
The following snippet would just reproduce the default values:
```csharp
IRetryPolicy retryPolicy = new ExponentialBackoff(
    int.MaxValue, TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(100));
deviceClient.SetRetryPolicy(retryPolicy);

deviceClient.OperationTimeoutInMilliseconds = DeviceClient.DefaultOperationTimeoutInMilliseconds; // 4min
```

The RetryPolicy is described here:
* [DeviceClient.SetRetryPolicy(IRetryPolicy)](https://docs.microsoft.com/en-us/dotnet/api/microsoft.azure.devices.client.deviceclient.setretrypolicy?view=azure-dotnet)
* [Default RetryPolicy](https://github.com/Azure/azure-iot-sdk-csharp/blob/master/iothub/device/devdoc/retrypolicy.md)

The [DeviceClient.OperationTimeoutInMilliseconds Property](https://docs.microsoft.com/en-us/dotnet/api/microsoft.azure.devices.client.deviceclient.operationtimeoutinmilliseconds?view=azure-dotnet) stores the timeout used in the operation retries and has a [DeviceClient.DefaultOperationTimeoutInMilliseconds](https://docs.microsoft.com/en-us/dotnet/api/microsoft.azure.devices.client.deviceclient.defaultoperationtimeoutinmilliseconds?view=azure-dotnet) of 4 minutes.

> Note that this value is ignored for operations where a cancellation token is provided. For example, `SendEventAsync(Message)` will use this timeout, but `SendEventAsync(Message, CancellationToken)` will not. The latter operation will only be cancelled by the provided cancellation token.