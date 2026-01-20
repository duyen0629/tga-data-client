using System.Runtime.Serialization;

namespace TgaGateway2.Models
{
    /// <summary>
    /// Flattened RTO classification scheme record for saving to Supabase
    /// </summary>
    [DataContract]
    public class RtoClassificationSchemeRecord
    {
        [DataMember]
        public string SchemeCode { get; set; }

        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public string Description { get; set; }

        [DataMember]
        public bool AllowMultipleValues { get; set; }

        [DataMember]
        public bool IsProtected { get; set; }

        [DataMember]
        public bool IsRequired { get; set; }

        [DataMember]
        public int ClassificationValuesCount { get; set; }

        public ExtensionDataObject ExtensionData { get; set; }
    }
}
