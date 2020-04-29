using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace IoTHubObserver
{
    class Config
    {
        [JsonProperty("clientId")]
        public string ClientId { get; set; }

        [JsonProperty("clientSecret")]
        public string ClientSecret { get; set; }

        [JsonProperty("subscriptionId")]
        public string SubscriptionId { get; set; }

        [JsonProperty("tenantId")]
        public string TenantId { get; set; }

        [JsonProperty("resourceGroupName")]
        public string ResourceGroupName { get; set; }

        [JsonProperty("iotHubName")]
        public string IotHubName { get; set; }
    }
}
