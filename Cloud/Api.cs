using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace API
{
    /// <summary>
    /// API Client Factory to manage HttpClient instances efficiently.
    /// </summary>
    public class APIClientFactory
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public APIClientFactory()
        {
            var services = new ServiceCollection();
            services.AddHttpClient();
            var serviceProvider = services.BuildServiceProvider();
            _httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
        }

        public HttpClient CreateClient() => _httpClientFactory.CreateClient();
    }

    /// <summary>
    /// API consumer client for interacting with ApiCommands methods.
    /// </summary>
    public class CloudServerWebUIApiCommandsClient
    {
        private readonly APIClientFactory _clientFactory;
        private readonly string _apiEntryPoint;

        /// <summary>
        /// Initializes a new instance of <see cref="CloudServerWebUIApiCommandsClient"/>.
        /// </summary>
        /// <param name="apiEntryPoint">Base API URL.</param>
        public CloudServerWebUIApiCommandsClient(string apiEntryPoint)
        {
            _clientFactory = new APIClientFactory();
            _apiEntryPoint = apiEntryPoint;
        }

        /// <summary>
        /// Create an instance of a new cloud in the root of the cloud space, and make it available to the infrastructure through the connection with the router
        /// </summary>
        /// <param name="name">A label name to assign to the cloud (this does not affect how the cloud works). This label, if unique, can be used as a search criterion</param>
        /// <param name="storageLimitGB">Limit cloud storage (useful for assigning storage to users with subscription plans)</param>
        /// <param name="endSubscription">If set, the service will be enabled until the set date</param>
        /// <returns>ID assigned to the new cloud</returns>
        public async Task<UInt64> CreateNewCloud(String name, Int32 storageLimitGB, DateTime endSubscription)
        {
            using var httpClient = _clientFactory.CreateClient();
            var requestData = new
            {
                name = name,
                storageLimitGB = storageLimitGB,
                endSubscription = endSubscription,
            };
            var jsonContent = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json");
            using var response = await httpClient.PostAsync(_apiEntryPoint + "/createnewcloud", jsonContent).ConfigureAwait(false);
            var responseData = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(responseData)) return default;
            return JsonDocument.Parse(responseData).RootElement.GetProperty("result").GetUInt64();
        }

        /// <summary>
        /// Set or change the expiration date of a cloud
        /// </summary>
        /// <param name="cloudId">Id of cloud to set</param>
        /// <param name="endSubscription">The service will be enabled until the date specified with this value</param>
        /// <returns>True id successful</returns>
        public async Task<Boolean> SetEndSubscription(UInt64 cloudId, DateTime endSubscription)
        {
            using var httpClient = _clientFactory.CreateClient();
            var requestData = new
            {
                cloudId = cloudId,
                endSubscription = endSubscription,
            };
            var jsonContent = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json");
            using var response = await httpClient.PostAsync(_apiEntryPoint + "/setendsubscription", jsonContent).ConfigureAwait(false);
            var responseData = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(responseData)) return default;
            return JsonDocument.Parse(responseData).RootElement.GetProperty("result").GetBoolean();
        }

        /// <summary>
        /// Create a new cloud service subscription
        /// Note: If the cloud with the subscription ID already exists, it will not be created and the subscription period for the existing cloud will be extended.
        /// </summary>
        /// <param name="idHex">Subscription Id (hex value) (also used as name/label for cloud to make it easier to find)</param>
        /// <param name="email">Email address to send the QR code for the connection (customer email address)</param>
        /// <param name="storageSpaceGb">Space required in GB</param>
        /// <param name="durationOfSubscriptionInDays">Day duration of subscription</param>
        /// <returns>Returns the encrypted connection QR code and cloud ID</returns>
        public async Task<JsonDocument> CreateNewSubscription(String idHex, String email, Int32 storageSpaceGb, Int32 durationOfSubscriptionInDays)
        {
            using var httpClient = _clientFactory.CreateClient();
            var requestData = new
            {
                idHex = idHex,
                email = email,
                storageSpaceGb = storageSpaceGb,
                durationOfSubscriptionInDays = durationOfSubscriptionInDays,
            };
            var jsonContent = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json");
            using var response = await httpClient.PostAsync(_apiEntryPoint + "/createnewsubscription", jsonContent).ConfigureAwait(false);
            var responseData = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(responseData)) return default;
            return JsonDocument.Parse(responseData);
        }

        /// <summary>
        /// Create a new cloud service subscription
        /// Note: If the cloud with the subscription ID already exists, it will not be created and the subscription period for the existing cloud will be extended.
        /// </summary>
        /// <param name="idHex">Subscription Id (hex value) (also used as name/label for cloud to make it easier to find)</param>
        /// <param name="storageSpaceGb">Space required in GB</param>
        /// <param name="durationOfSubscriptionInDays">Day duration of subscription</param>
        /// <returns>Returns the encrypted connection QR code and cloud ID</returns>
        public async Task<JsonDocument> CreateNewSubscription(String idHex, Int32 storageSpaceGb, Int32 durationOfSubscriptionInDays)
        {
            using var httpClient = _clientFactory.CreateClient();
            var requestData = new
            {
                idHex = idHex,
                storageSpaceGb = storageSpaceGb,
                durationOfSubscriptionInDays = durationOfSubscriptionInDays,
            };
            var jsonContent = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json");
            using var response = await httpClient.PostAsync(_apiEntryPoint + "/createnewsubscription", jsonContent).ConfigureAwait(false);
            var responseData = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(responseData)) return default;
            return JsonDocument.Parse(responseData);
        }

        /// <summary>
        /// Create a new cloud service subscription
        /// Note: If the cloud with the subscription ID already exists, it will not be created and the subscription period for the existing cloud will be extended.
        /// </summary>
        /// <param name="storageSpaceGb">Space required in GB</param>
        /// <param name="durationOfSubscriptionInDays">Day duration of subscription</param>
        /// <returns>Returns the encrypted connection QR code and cloud ID</returns>
        public async Task<JsonDocument> CreateNewSubscription(Int32 storageSpaceGb, Int32 durationOfSubscriptionInDays)
        {
            using var httpClient = _clientFactory.CreateClient();
            var requestData = new
            {
                storageSpaceGb = storageSpaceGb,
                durationOfSubscriptionInDays = durationOfSubscriptionInDays,
            };
            var jsonContent = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json");
            using var response = await httpClient.PostAsync(_apiEntryPoint + "/createnewsubscription", jsonContent).ConfigureAwait(false);
            var responseData = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(responseData)) return default;
            return JsonDocument.Parse(responseData);
        }

    }
}