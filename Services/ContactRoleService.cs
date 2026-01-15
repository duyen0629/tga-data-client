using System;
using System.ServiceModel;
using training.gov.au.services;

namespace TgaGateway2.Services
{
    /// <summary>
    /// Service for fetching TrainingComponentContactRole data from TGA (Training.gov.au) web services
    /// </summary>
    public class ContactRoleService : IDisposable
    {
        private TrainingComponentServiceClient _trainingComponentClient;
        private bool _disposed = false;

        /// <summary>
        /// Initializes a new instance of ContactRoleService with default credentials
        /// </summary>
        public ContactRoleService()
        {
            _trainingComponentClient = new TrainingComponentServiceClient("TrainingComponentServiceBasicHttpEndpoint");
            _trainingComponentClient.ClientCredentials.UserName.UserName = "WebService.Read";
            _trainingComponentClient.ClientCredentials.UserName.Password = "Asdf098";
        }

        /// <summary>
        /// Initializes a new instance of ContactRoleService with custom credentials
        /// </summary>
        public ContactRoleService(string username, string password)
        {
            _trainingComponentClient = new TrainingComponentServiceClient("TrainingComponentServiceBasicHttpEndpoint");
            _trainingComponentClient.ClientCredentials.UserName.UserName = username;
            _trainingComponentClient.ClientCredentials.UserName.Password = password;
        }

        /// <summary>
        /// Gets all contact roles from TGA service
        /// </summary>
        public TrainingComponentContactRole[] GetContactRoles()
        {
            EnsureNotDisposed();
            return _trainingComponentClient.GetContactRoles();
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ContactRoleService));
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
