using System;
using System.ServiceModel;
using training.gov.au.services;

namespace TgaGateway2.Services
{
    /// <summary>
    /// Service for fetching Release data via GetDetails
    /// </summary>
    public class ReleaseService : IDisposable
    {
        private TrainingComponentServiceClient _trainingComponentClient;
        private bool _disposed = false;

        /// <summary>
        /// Initializes a new instance with default credentials
        /// </summary>
        public ReleaseService()
        {
            _trainingComponentClient = new TrainingComponentServiceClient("TrainingComponentServiceBasicHttpEndpoint");
            TgaSoapConfig.ApplyUserNameCredentials(_trainingComponentClient.ClientCredentials);
        }

        /// <summary>
        /// Initializes a new instance with custom credentials
        /// </summary>
        public ReleaseService(string username, string password)
        {
            _trainingComponentClient = new TrainingComponentServiceClient("TrainingComponentServiceBasicHttpEndpoint");
            _trainingComponentClient.ClientCredentials.UserName.UserName = username;
            _trainingComponentClient.ClientCredentials.UserName.Password = password;
        }

        /// <summary>
        /// Gets releases for a training component code
        /// </summary>
        public Release[] GetReleases(string trainingComponentCode)
        {
            EnsureNotDisposed();

            var request = new TrainingComponentDetailsRequest
            {
                Code = trainingComponentCode,
                InformationRequest = new TrainingComponentInformationRequested
                {
                    ShowReleases = true,
                    ShowUnitGrid = true,
                    ShowComponents = true,
                    ShowFiles = true
                }
            };

            var details = _trainingComponentClient.GetDetails(request);
            return details?.Releases ?? Array.Empty<Release>();
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ReleaseService));
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
