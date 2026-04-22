using System;
using System.ServiceModel;
using training.gov.au.services;

namespace TgaGateway2.Services
{
    /// <summary>
    /// Service for fetching classification purposes from TGA (Training.gov.au) web services
    /// </summary>
    public class ClassificationPurposeService : IDisposable
    {
        private TrainingComponentServiceClient _trainingComponentClient;
        private bool _disposed = false;

        /// <summary>
        /// Initializes a new instance of ClassificationPurposeService with default credentials
        /// </summary>
        public ClassificationPurposeService()
        {
            _trainingComponentClient = new TrainingComponentServiceClient("TrainingComponentServiceBasicHttpEndpoint");
            TgaSoapConfig.ApplyUserNameCredentials(_trainingComponentClient.ClientCredentials);
        }

        /// <summary>
        /// Initializes a new instance of ClassificationPurposeService with custom credentials
        /// </summary>
        public ClassificationPurposeService(string username, string password)
        {
            _trainingComponentClient = new TrainingComponentServiceClient("TrainingComponentServiceBasicHttpEndpoint");
            _trainingComponentClient.ClientCredentials.UserName.UserName = username;
            _trainingComponentClient.ClientCredentials.UserName.Password = password;
        }

        /// <summary>
        /// Gets all classification purposes from TGA service
        /// </summary>
        public ClassificationPurpose[] GetClassificationPurposes()
        {
            EnsureNotDisposed();
            return _trainingComponentClient.GetClassificationPurposes();
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ClassificationPurposeService));
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
