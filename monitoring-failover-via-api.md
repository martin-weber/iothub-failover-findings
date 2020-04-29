# Monitoring IoT Hub failover via API

A consumer which connects to the built-in Event Hub compatible endpoint of the IoT Hub is affected by an IoT Hub failover and needs to properly react.

This article focuses on monitoring the IoT Hub via the Azure Management API. Another solution which addresses message loss challenge related to failover handling with EventProcessors can be found in [Minimize message loss](minimize-messageloss.md).

Both these examples are integrating the checks to Azure management API. In an ideal production scenario, you may want to have an external orchestrator for the control plane.

# Observing the IoT Hub to detect failover events
To observe the IoT Hub on the consumer side, the Azure Management SDK can be used to get the address of the built-in endpoint. The relevant classes for IoT Hub are in the namespaces:  
* [Microsoft.Azure.Management.IotHub](https://docs.microsoft.com/en-us/dotnet/api/microsoft.azure.management.iothub?view=azure-dotnet) 
* [Microsoft.Azure.Management.IotHub.Models](https://docs.microsoft.com/en-us/dotnet/api/microsoft.azure.management.iothub.models?view=azure-dotnet).  

It requires the NuGet packages:
* [Microsoft.Azure.Management.IotHub](https://www.nuget.org/packages/Microsoft.Azure.Management.IotHub/) containing the models 
* [Microsoft.Rest.ClientRuntime.Azure.Authentication](https://www.nuget.org/packages/Microsoft.Rest.ClientRuntime.Azure.Authentication/) for the authentication

## Connecting using the management API

To access the IoT Hub Models via APIs, an `IotHubClient` instance is created by login to the AAD tenant by using the service principal created before. The  `IotHubClient` needs the `SubscriptionId` to be set.
The `IotHubClient` is then used to further access the different IoT Hub Management APIs. The `Config` class maps the appsettings.json.

```csharp
using Microsoft.Azure.Management.IotHub;
using Microsoft.Azure.Management.IotHub.Models;
using Microsoft.Rest.Azure.Authentication;

...

private static async Task<IotHubClient> GetIotHubClientAsync(Config config)
{
    var serviceClientCredentials = await ApplicationTokenProvider.LoginSilentAsync(config.TenantId, config.ClientId, config.ClientSecret);
    var iotHubClient = new IotHubClient(serviceClientCredentials);
    iotHubClient.SubscriptionId = config.SubscriptionId;
    return iotHubClient;
}
```

## Getting the IoT Hub Model

The following code snippet shows the code to retrieve the [IotHubDescription](https://docs.microsoft.com/en-us/dotnet/api/microsoft.azure.management.iothub.models.iothubdescription?view=azure-dotnet) which contains most of the properties.

```csharp
private static async Task<IotHubDescription> GetIotHubDescriptionAsync(IotHubClient iotHubClient, Config config, CancellationToken cancellationToken = default)
{
    IotHubDescription iotHubDescription = await iotHubClient.IotHubResource.GetAsync(config.ResourceGroupName, config.IotHubName, cancellationToken);
    return iotHubDescription;
}
```

The `IotHubDescription` provides in its `Properties` the `Status` and the built-in `EventHubEndpoint` called _events_:

```csharp
IotHubDescription iotHub = await GetIoTHubDescriptionAsync();
string currentState = iotHub.Properties.State;
string currentEndpointAddress = iotHub.Properties.EventHubEndpoints["events"].Endpoint;
string currentPath = iotHub.Properties.EventHubEndpoints["events"].Path;
```

## Rebuilding the Connection string

Different connection strings are used by different functions of the IoT Hub.
The IoT Hub itself has some connection strings found in the portal under _Shared access policies_. 
From a consumer cloud apps perspective, the most important one is the _iothubowner_:

```
HostName=<NAME-OF-THE-IOTHUB>.azure-devices.net;
SharedAccessKeyName=iothubowner;
SharedAccessKey=<IOT HUB OWNER PRIMARY KEY>
```

The consumer uses the built-in events endpoint, whose connection string can be found in the portal under _Built-in endpoints_.
The connection string for the built-in events endpoint has different parts:
1. The __endpoint address__, contains a unique suffix. This suffix changes during failover.
    ```
    Endpoint=sb://iothub-ns-<NAME>-<SUFFIX>.servicebus.windows.net/;
    ```
2. The __Shared Access policy__, is the same as the one for the IoT Hub:
    ``` 
    SharedAccessKeyName=iothubowner;
    SharedAccessKey=<IOT HUB OWNER PRIMARY KEY>;
    ```

3. The __EntityPath__ is the name of the built-in Event Hub:
    ```
    EntityPath=<NAME-OF-THE-IOTHUB>
    ```

The connection string for the built-in endpoint used by the consumer can be combined using the endpoint address, the entity path and the `iothubowner` Shared Access Key fetched all using the management API.

The Shared Access Signature for the `iothubowner` containing the keys can be retrieved using the API as shown in following snippet:

```csharp
private static async Task<SharedAccessSignatureAuthorizationRule> GetIotHubKeyAsync(IotHubClient iotHubClient, Config config, CancellationToken cancellationToken)
{
    const string iotHubSharedAccessKeyName = "iothubowner";
    var keys = await iotHubClient.IotHubResource.ListKeysAsync(config.ResourceGroupName, config.IotHubName, cancellationToken);
    var sharedAccessSignatureAuthorizationRule = keys.Single(k => k.KeyName == iotHubSharedAccessKeyName);
    return sharedAccessSignatureAuthorizationRule;
}
```

The following snippet contains a sample, how to build the connection string based on the `IotHubDecription` fetched using the `GetIoTHubAsync(...)` method shown above and the SharedAccessKeyName and Value fetched from secure configuration storage.

```csharp
private static string BuildEventHubConnectionString(IotHubDescription iotHubDescription, SharedAccessSignatureAuthorizationRule sasAuthorizationRule)
{
    var endpointAddress = iotHubDescription.Properties.EventHubEndpoints["events"].Endpoint;
    var entityName = iotHubDescription.Properties.EventHubEndpoints["events"].Path;
    var builder = new EventHubsConnectionStringBuilder(
                        new Uri(endpointAddress), entityName,
                        sasAuthorizationRule.KeyName, sasAuthorizationRule.PrimaryKey);
    return builder.ToString();
}
```
# Sample

The related sample code for monitoring IoT Hub for failover using the management API can be found in the [samples/IoTHubObserver](./samples/IotHubObserver/) folder.

For the initial setup of this example the following steps are required:
1. Create a resource group with an IoT Hub in your subscription and add the names to the corresponding fields in appsettings.json.
2. Connect to your subscription and execute the following command using Azure-CLI or Cloud-Shell to set up the service principal:
   ```
   az ad sp create-for-rbac --sdk-auth
   ```
   From the JSON output of this command, copy the first four lines for `clientId`, `clientSecret`, `subscriptionId` and `tenantId` to the appsettings.json.
3. Now you should have filled the missing values in appsettings.json file:
   ```json
   {
     "clientId": "<from-service-principal - step 2>",
     "clientSecret": "<from-service-principal - step 2>",
     "subscriptionId": "<from-service-principal - step 2>",
     "tenantId": "<from-service-principal - step 2>",
   
     "resourceGroupName": "<the-resource-group-name - step 1>",
     "iotHubName": "<the-iot-hub-name - step 1>"
   }
   ```