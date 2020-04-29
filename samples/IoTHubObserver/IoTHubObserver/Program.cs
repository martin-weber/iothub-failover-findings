using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.Management.IotHub;
using Microsoft.Azure.Management.IotHub.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Rest.Azure.Authentication;

namespace IoTHubObserver
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var builder = new ConfigurationBuilder()
                .AddJsonFile($"appsettings.json", true, false)
                .AddJsonFile($"appsettings.local.json", true, false)
                .AddEnvironmentVariables();
            var configuration = builder.Build();
            
            var config = configuration.Get<Config>();
            var cancellationTokenSource = new CancellationTokenSource();
            Task task =  ObserveIoTHub(config, cancellationTokenSource.Token);
            
            Console.WriteLine("Press enter to cancel");
            Console.ReadLine();
            cancellationTokenSource.Cancel();
        }

        private static async Task ObserveIoTHub(Config config, CancellationToken cancellationToken)
        {
            string previousState = null;
            string previousEndpointAddress = null;

            IotHubClient iotHubClient = await GetIotHubClientAsync(config);
            SharedAccessSignatureAuthorizationRule sasAuthorizationRule = await GetIotHubKeyAsync(iotHubClient, config, cancellationToken);

            while (!cancellationToken.IsCancellationRequested) 
            {
                IotHubDescription iotHubDescription = await GetIotHubDescriptionAsync(iotHubClient, config, cancellationToken);

                var currentState = iotHubDescription.Properties.State;
                var currentEndpointAddress = iotHubDescription.Properties.EventHubEndpoints["events"].Endpoint;
                var currentEndpointPath = iotHubDescription.Properties.EventHubEndpoints["events"].Path;

                var stateChanged = currentState != previousState;
                var endpointChanged = currentEndpointAddress != previousEndpointAddress;

                if (stateChanged || endpointChanged)
                {
                    // print changes
                    Console.WriteLine();
                    Console.WriteLine($"-----------------------------------------------------------");
                    Console.WriteLine($"Date time:             {DateTime.Now}");
                    Console.WriteLine($"Status changed:        {previousState} => {currentState}");
                    Console.WriteLine($"Endpoint Address:      {currentEndpointAddress}");
                    Console.WriteLine($"Endpoint Path:         {currentEndpointPath}");

                    if (endpointChanged)
                    {
                        var connectionString = BuildEventHubConnectionString(iotHubDescription, sasAuthorizationRule);
                        Console.WriteLine($"Endpoint new/changed");
                        Console.WriteLine($"new connection string: {connectionString}");
                    }
                }
                else
                {
                    // print indicator that application is running
                    Console.Write(".");
                }

                previousState = currentState;
                previousEndpointAddress = currentEndpointAddress;

                await Task.Delay(1000);
            }
        }

        private static async Task<IotHubClient> GetIotHubClientAsync(Config config)
        {
            var serviceClientCredentials = await ApplicationTokenProvider.LoginSilentAsync(config.TenantId, config.ClientId, config.ClientSecret);

            var iotHubClient = new IotHubClient(serviceClientCredentials);
            iotHubClient.SubscriptionId = config.SubscriptionId;
            return iotHubClient;
        }

        private static async Task<IotHubDescription> GetIotHubDescriptionAsync(IotHubClient iotHubClient, Config config, CancellationToken cancellationToken = default)
        {
            IotHubDescription iotHubDescription = await iotHubClient.IotHubResource.GetAsync(config.ResourceGroupName, config.IotHubName, cancellationToken);
            return iotHubDescription;
        }

        private static async Task<SharedAccessSignatureAuthorizationRule> GetIotHubKeyAsync(IotHubClient iotHubClient, Config config, CancellationToken cancellationToken)
        {
            const string iotHubSharedAccessKeyName = "iothubowner";
            var keys = await iotHubClient.IotHubResource.ListKeysAsync(config.ResourceGroupName, config.IotHubName, cancellationToken);
            var sharedAccessSignatureAuthorizationRule = keys.Single(k => k.KeyName == iotHubSharedAccessKeyName);
            return sharedAccessSignatureAuthorizationRule;
        }

        private static string BuildEventHubConnectionString(IotHubDescription iotHubDescription, SharedAccessSignatureAuthorizationRule sasAuthorizationRule)
        {
            var endpointAddress = iotHubDescription.Properties.EventHubEndpoints["events"].Endpoint;
            var entityName = iotHubDescription.Properties.EventHubEndpoints["events"].Path;
            var builder = new EventHubsConnectionStringBuilder(new Uri(endpointAddress), entityName, sasAuthorizationRule.KeyName, sasAuthorizationRule.PrimaryKey);
            return builder.ToString();
        }
    }
}
