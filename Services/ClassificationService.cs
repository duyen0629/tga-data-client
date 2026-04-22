using System;
using System.ServiceModel;
using training.gov.au.services;

namespace TgaGateway2.Services
{
    /// <summary>
    /// Service for fetching Classification data via GetDetails
    /// </summary>
    public class ClassificationService : IDisposable
    {
        private TrainingComponentServiceClient _trainingComponentClient;
        private bool _disposed = false;

        /// <summary>
        /// Initializes a new instance with default credentials
        /// </summary>
        public ClassificationService()
        {
            _trainingComponentClient = new TrainingComponentServiceClient("TrainingComponentServiceBasicHttpEndpoint");
            TgaSoapConfig.ApplyUserNameCredentials(_trainingComponentClient.ClientCredentials);
        }

        /// <summary>
        /// Initializes a new instance with custom credentials
        /// </summary>
        public ClassificationService(string username, string password)
        {
            _trainingComponentClient = new TrainingComponentServiceClient("TrainingComponentServiceBasicHttpEndpoint");
            _trainingComponentClient.ClientCredentials.UserName.UserName = username;
            _trainingComponentClient.ClientCredentials.UserName.Password = password;
        }

        /// <summary>
        /// Gets classifications for a training component code
        /// </summary>
        public Classification[] GetClassifications(string trainingComponentCode)
        {
            EnsureNotDisposed();

            var request = new TrainingComponentDetailsRequest
            {
                Code = trainingComponentCode,
                InformationRequest = new TrainingComponentInformationRequested
                {
                    ShowClassifications = true
                }
            };

            var details = _trainingComponentClient.GetDetails(request);
            return details?.Classifications ?? Array.Empty<Classification>();
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ClassificationService));
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
