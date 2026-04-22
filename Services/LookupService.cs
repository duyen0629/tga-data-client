using System;
using System.ServiceModel;
using training.gov.au.services;

namespace TgaGateway2.Services
{
    /// <summary>
    /// Service for fetching lookup data from TGA (Training.gov.au) web services
    /// </summary>
    public class LookupService : IDisposable
    {
        private TrainingComponentServiceClient _trainingComponentClient;
        private bool _disposed = false;

        /// <summary>
        /// Initializes a new instance of LookupService with default credentials
        /// </summary>
        public LookupService()
        {
            _trainingComponentClient = new TrainingComponentServiceClient("TrainingComponentServiceBasicHttpEndpoint");
            TgaSoapConfig.ApplyUserNameCredentials(_trainingComponentClient.ClientCredentials);
        }

        /// <summary>
        /// Initializes a new instance of LookupService with custom credentials
        /// </summary>
        public LookupService(string username, string password)
        {
            _trainingComponentClient = new TrainingComponentServiceClient("TrainingComponentServiceBasicHttpEndpoint");
            _trainingComponentClient.ClientCredentials.UserName.UserName = username;
            _trainingComponentClient.ClientCredentials.UserName.Password = password;
        }

        /// <summary>
        /// Gets lookup values from TGA service for the given lookup name
        /// </summary>
        public Lookup[] GetLookup(LookupName lookupName)
        {
            EnsureNotDisposed();
            var request = new LookupRequest { LookupName = lookupName };
            return _trainingComponentClient.GetLookup(request);
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(LookupService));
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
