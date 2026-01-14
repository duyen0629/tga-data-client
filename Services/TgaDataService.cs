using System;
using System.ServiceModel;
using training.gov.au.services;

namespace TgaGateway2.Services
{
    /// <summary>
    /// Service for fetching data from TGA (Training.gov.au) web services
    /// </summary>
    public class TgaDataService : IDisposable
    {
        private TrainingComponentServiceClient _trainingComponentClient;
        private bool _disposed = false;

        /// <summary>
        /// Initializes a new instance of TgaDataService with default credentials
        /// </summary>
        public TgaDataService()
        {
            _trainingComponentClient = new TrainingComponentServiceClient("TrainingComponentServiceBasicHttpEndpoint");
            _trainingComponentClient.ClientCredentials.UserName.UserName = "WebService.Read";
            _trainingComponentClient.ClientCredentials.UserName.Password = "Asdf098";
        }

        /// <summary>
        /// Initializes a new instance of TgaDataService with custom credentials
        /// </summary>
        public TgaDataService(string username, string password)
        {
            _trainingComponentClient = new TrainingComponentServiceClient("TrainingComponentServiceBasicHttpEndpoint");
            _trainingComponentClient.ClientCredentials.UserName.UserName = username;
            _trainingComponentClient.ClientCredentials.UserName.Password = password;
        }

        /// <summary>
        /// Gets the server time from TGA service
        /// </summary>
        public DateTime GetServerTime()
        {
            EnsureNotDisposed();
            return _trainingComponentClient.GetServerTime();
        }

        /// <summary>
        /// Gets all recognition managers from TGA service
        /// </summary>
        public RecognitionManager[] GetRecognitionManagers()
        {
            EnsureNotDisposed();
            return _trainingComponentClient.GetRecognitionManagers();
        }

        /// <summary>
        /// Gets training component details for a specific code
        /// </summary>
        public TrainingComponent GetTrainingComponentDetails(string code, bool showReleases = true,
            bool showRecognitionManagers = true, bool showContacts = true, bool showClassifications = true)
        {
            EnsureNotDisposed();

            var request = new TrainingComponentDetailsRequest
            {
                Code = code,
                InformationRequest = new TrainingComponentInformationRequested
                {
                    ShowReleases = showReleases,
                    ShowRecognitionManagers = showRecognitionManagers,
                    ShowContacts = showContacts,
                    ShowClassifications = showClassifications
                }
            };

            return _trainingComponentClient.GetDetails(request);
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(TgaDataService));
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
