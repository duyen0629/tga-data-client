using System.Runtime.Serialization;

namespace TgaGateway2.Models
{
    /// <summary>
    /// Flattened organisation summary record for saving to Supabase
    /// </summary>
    [DataContract]
    public class OrganisationSummaryRecord
    {
        [DataMember]
        public string Code { get; set; }

        [DataMember]
        public string DataManagerCode { get; set; }

        [DataMember]
        public bool HasActiveRegistration { get; set; }

        [DataMember]
        public string LegalPersonName { get; set; }

        [DataMember]
        public string TradingName { get; set; }

        [DataMember]
        public System.DateTimeOffset UpdatedDate { get; set; }

        [DataMember]
        public bool IsLegacyData { get; set; }

        [DataMember]
        public string RegistrationStatus { get; set; }

        public ExtensionDataObject ExtensionData { get; set; }
    }
}
