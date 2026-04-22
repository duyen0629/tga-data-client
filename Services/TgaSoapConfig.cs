using System.Configuration;
using System.ServiceModel.Description;

namespace TgaGateway2.Services
{
    internal static class TgaSoapConfig
    {
        /// <summary>
        /// Applies SOAP message credentials from App.config (TgaSoapUserName / TgaSoapPassword).
        /// Falls back to sandbox demo credentials when keys are absent.
        /// </summary>
        public static void ApplyUserNameCredentials(ClientCredentials credentials)
        {
            credentials.UserName.UserName = ConfigurationManager.AppSettings["TgaSoapUserName"] ?? "WebService.Read";
            credentials.UserName.Password = ConfigurationManager.AppSettings["TgaSoapPassword"] ?? "Asdf098";
        }
    }
}
