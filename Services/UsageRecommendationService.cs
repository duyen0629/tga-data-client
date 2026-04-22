using System;
using System.ServiceModel;
using training.gov.au.services;

namespace TgaGateway2.Services
{
    /// <summary>
    /// Service for fetching Usage Recommendation data via GetDetails
    /// </summary>
    public class UsageRecommendationService : IDisposable
    {
        private TrainingComponentServiceClient _trainingComponentClient;
        private bool _disposed = false;

        /// <summary>
        /// Initializes a new instance with default credentials
        /// </summary>
        public UsageRecommendationService()
        {
            _trainingComponentClient = new TrainingComponentServiceClient("TrainingComponentServiceBasicHttpEndpoint");
            TgaSoapConfig.ApplyUserNameCredentials(_trainingComponentClient.ClientCredentials);
        }

        /// <summary>
        /// Initializes a new instance with custom credentials
        /// </summary>
        public UsageRecommendationService(string username, string password)
        {
            _trainingComponentClient = new TrainingComponentServiceClient("TrainingComponentServiceBasicHttpEndpoint");
            _trainingComponentClient.ClientCredentials.UserName.UserName = username;
            _trainingComponentClient.ClientCredentials.UserName.Password = password;
        }

        /// <summary>
        /// Gets usage recommendations for a training component code
        /// </summary>
        public UsageRecommendation[] GetUsageRecommendations(string trainingComponentCode)
        {
            EnsureNotDisposed();

            var request = new TrainingComponentDetailsRequest
            {
                Code = trainingComponentCode,
                InformationRequest = new TrainingComponentInformationRequested
                {
                    ShowUsageRecommendation = true
                }
            };

            var details = _trainingComponentClient.GetDetails(request);
            return details?.UsageRecommendations ?? Array.Empty<UsageRecommendation>();
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(UsageRecommendationService));
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _trainingComponentClient?.Close();
                _trainingComponentClient = null;
                _disposed = true;
            }
        }
    }
}
