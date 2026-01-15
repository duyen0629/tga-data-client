using System;
using System.ServiceModel;
using training.gov.au.services;

namespace TgaGateway2.Services
{
    /// <summary>
    /// Service for fetching RecognitionManagerAssignment data via GetDetails
    /// </summary>
    public class RecognitionManagerAssignmentService : IDisposable
    {
        private TrainingComponentServiceClient _trainingComponentClient;
        private bool _disposed = false;

        /// <summary>
        /// Initializes a new instance with default credentials
        /// </summary>
        public RecognitionManagerAssignmentService()
        {
            _trainingComponentClient = new TrainingComponentServiceClient("TrainingComponentServiceBasicHttpEndpoint");
            _trainingComponentClient.ClientCredentials.UserName.UserName = "WebService.Read";
            _trainingComponentClient.ClientCredentials.UserName.Password = "Asdf098";
        }

        /// <summary>
        /// Initializes a new instance with custom credentials
        /// </summary>
        public RecognitionManagerAssignmentService(string username, string password)
        {
            _trainingComponentClient = new TrainingComponentServiceClient("TrainingComponentServiceBasicHttpEndpoint");
            _trainingComponentClient.ClientCredentials.UserName.UserName = username;
            _trainingComponentClient.ClientCredentials.UserName.Password = password;
        }

        /// <summary>
        /// Gets recognition manager assignments for a training component code
        /// </summary>
        public RecognitionManagerAssignment[] GetRecognitionManagerAssignments(string trainingComponentCode)
        {
            EnsureNotDisposed();

            var request = new TrainingComponentDetailsRequest
            {
                Code = trainingComponentCode,
                InformationRequest = new TrainingComponentInformationRequested
                {
                    ShowRecognitionManagers = true
                }
            };

            var details = _trainingComponentClient.GetDetails(request);
            return details?.RecognitionManagers ?? Array.Empty<RecognitionManagerAssignment>();
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(RecognitionManagerAssignmentService));
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
