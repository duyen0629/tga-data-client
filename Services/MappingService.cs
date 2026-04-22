using System;
using System.ServiceModel;
using training.gov.au.services;

namespace TgaGateway2.Services
{
    /// <summary>
    /// Service for fetching Mapping data via GetDetails
    /// </summary>
    public class MappingService : IDisposable
    {
        private TrainingComponentServiceClient _trainingComponentClient;
        private bool _disposed = false;

        /// <summary>
        /// Initializes a new instance with default credentials
        /// </summary>
        public MappingService()
        {
            _trainingComponentClient = new TrainingComponentServiceClient("TrainingComponentServiceBasicHttpEndpoint");
            TgaSoapConfig.ApplyUserNameCredentials(_trainingComponentClient.ClientCredentials);
        }

        /// <summary>
        /// Initializes a new instance with custom credentials
        /// </summary>
        public MappingService(string username, string password)
        {
            _trainingComponentClient = new TrainingComponentServiceClient("TrainingComponentServiceBasicHttpEndpoint");
            _trainingComponentClient.ClientCredentials.UserName.UserName = username;
            _trainingComponentClient.ClientCredentials.UserName.Password = password;
        }

        /// <summary>
        /// Gets mapping information for a training component code
        /// </summary>
        public Mapping[] GetMappings(string trainingComponentCode)
        {
            EnsureNotDisposed();

            var request = new TrainingComponentDetailsRequest
            {
                Code = trainingComponentCode,
                InformationRequest = new TrainingComponentInformationRequested
                {
                    ShowMappingInformation = true
                }
            };

            var details = _trainingComponentClient.GetDetails(request);
            return details?.MappingInformation ?? Array.Empty<Mapping>();
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(MappingService));
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
