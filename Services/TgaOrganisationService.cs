using System;

namespace TgaGateway2.Services
{
    /// <summary>
    /// Service for accessing TGA OrganisationService endpoints
    /// </summary>
    public class TgaOrganisationService : IDisposable
    {
        private readonly System.ServiceModel.ChannelFactory<IOrganisationService> _channelFactory;
        private readonly IOrganisationService _organisationChannel;
        private bool _disposed = false;

        public TgaOrganisationService()
        {
            _channelFactory = new System.ServiceModel.ChannelFactory<IOrganisationService>("OrganisationServiceBasicHttpEndpoint");
            _channelFactory.Credentials.UserName.UserName = "WebService.Read";
            _channelFactory.Credentials.UserName.Password = "Asdf098";
            _organisationChannel = _channelFactory.CreateChannel();
        }

        public TgaOrganisationService(string username, string password)
        {
            _channelFactory = new System.ServiceModel.ChannelFactory<IOrganisationService>("OrganisationServiceBasicHttpEndpoint");
            _channelFactory.Credentials.UserName.UserName = username;
            _channelFactory.Credentials.UserName.Password = password;
            _organisationChannel = _channelFactory.CreateChannel();
        }

        public training.gov.au.services.OrganisationSearchResult SearchByModifiedDate(training.gov.au.services.OrganisationModifiedSearchRequest request)
        {
            EnsureNotDisposed();
            return _organisationChannel.SearchByModifiedDate(request);
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(TgaOrganisationService));
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                try
                {
                    var client = _organisationChannel as System.ServiceModel.IClientChannel;
                    client?.Close();
                }
                catch
                {
                    var client = _organisationChannel as System.ServiceModel.IClientChannel;
                    client?.Abort();
                }

                _channelFactory?.Close();
                _disposed = true;
            }
        }
    }
}
