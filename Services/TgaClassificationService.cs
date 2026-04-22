using System;
using training.gov.au.services;

namespace TgaGateway2.Services
{
    /// <summary>
    /// Service for accessing TGA ClassificationService endpoints
    /// </summary>
    public class TgaClassificationService : IDisposable
    {
        private readonly System.ServiceModel.ChannelFactory<IClassificationService> _channelFactory;
        private readonly IClassificationService _classificationChannel;
        private bool _disposed = false;

        /// <summary>
        /// Initializes a new instance of TgaClassificationService with default credentials
        /// </summary>
        public TgaClassificationService()
        {
            _channelFactory = new System.ServiceModel.ChannelFactory<IClassificationService>("ClassificationServiceBasicHttpEndpoint");
            TgaSoapConfig.ApplyUserNameCredentials(_channelFactory.Credentials);
            _classificationChannel = _channelFactory.CreateChannel();
        }

        /// <summary>
        /// Initializes a new instance of TgaClassificationService with custom credentials
        /// </summary>
        public TgaClassificationService(string username, string password)
        {
            _channelFactory = new System.ServiceModel.ChannelFactory<IClassificationService>("ClassificationServiceBasicHttpEndpoint");
            _channelFactory.Credentials.UserName.UserName = username;
            _channelFactory.Credentials.UserName.Password = password;
            _classificationChannel = _channelFactory.CreateChannel();
        }

        /// <summary>
        /// Searches NRT classifications by scheme code
        /// </summary>
        public NrtClassificationSchemeResult SearchNrtClassificationsByScheme(string schemeCode)
        {
            EnsureNotDisposed();
            return _classificationChannel.SearchNrtClassificationsByScheme(schemeCode);
        }

        /// <summary>
        /// Searches RTO classifications by scheme code
        /// </summary>
        public RtoClassificationSchemeResult SearchRtoClassificationsByScheme(string schemeCode)
        {
            EnsureNotDisposed();
            return _classificationChannel.SearchRtoClassificationsByScheme(schemeCode);
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(TgaClassificationService));
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                try
                {
                    var client = _classificationChannel as System.ServiceModel.IClientChannel;
                    client?.Close();
                }
                catch
                {
                    var client = _classificationChannel as System.ServiceModel.IClientChannel;
                    client?.Abort();
                }

                _channelFactory?.Close();
                _disposed = true;
            }
        }
    }
}
