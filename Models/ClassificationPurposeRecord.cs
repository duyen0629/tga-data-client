using System.Runtime.Serialization;

namespace TgaGateway2.Models
{
    /// <summary>
    /// Flattened classification purpose record for saving to Supabase
    /// </summary>
    [DataContract]
    public class ClassificationPurposeRecord
    {
        [DataMember]
        public string PurposeCode { get; set; }

        [DataMember]
        public string Description { get; set; }

        public ExtensionDataObject ExtensionData { get; set; }
    }
}
