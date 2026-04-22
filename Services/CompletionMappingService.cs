using System;
using System.ServiceModel;
using training.gov.au.services;

namespace TgaGateway2.Services
{
    /// <summary>
    /// Service for fetching Completion Mapping data via GetDetails
    /// </summary>
    public class CompletionMappingService : IDisposable
    {
        private TrainingComponentServiceClient _trainingComponentClient;
        private bool _disposed = false;

        /// <summary>
        /// Initializes a new instance with default credentials
        /// </summary>
        public CompletionMappingService()
        {
            _trainingComponentClient = new TrainingComponentServiceClient("TrainingComponentServiceBasicHttpEndpoint");
            TgaSoapConfig.ApplyUserNameCredentials(_trainingComponentClient.ClientCredentials);
        }

        /// <summary>
        /// Initializes a new instance with custom credentials
        /// </summary>
        public CompletionMappingService(string username, string password)
        {
            _trainingComponentClient = new TrainingComponentServiceClient("TrainingComponentServiceBasicHttpEndpoint");
            _trainingComponentClient.ClientCredentials.UserName.UserName = username;
            _trainingComponentClient.ClientCredentials.UserName.Password = password;
        }

        /// <summary>
        /// Gets completion mappings for a training component code
        /// </summary>
        public NrtCompletion[] GetCompletionMappings(string trainingComponentCode)
        {
            EnsureNotDisposed();

            var request = new TrainingComponentDetailsRequest
            {
                Code = trainingComponentCode,
                InformationRequest = new TrainingComponentInformationRequested
                {
                    ShowCompletionMapping = true
                }
            };

            var details = _trainingComponentClient.GetDetails(request);
            return details?.CompletionMapping ?? Array.Empty<NrtCompletion>();
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(CompletionMappingService));
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
