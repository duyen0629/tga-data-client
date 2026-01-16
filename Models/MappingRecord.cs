using System.Runtime.Serialization;

namespace TgaGateway2.Models
{
    /// <summary>
    /// Flattened mapping record for saving to Supabase
    /// </summary>
    [DataContract]
    public class MappingRecord
    {
        [DataMember]
        public string MappingKey { get; set; }

        [DataMember]
        public string TrainingComponentCode { get; set; }

        [DataMember]
        public string Code { get; set; }

        [DataMember]
        public bool IsEquivalent { get; set; }

        [DataMember]
        public string MapsToCode { get; set; }

        [DataMember]
        public string MapsToTitle { get; set; }

        [DataMember]
        public string Notes { get; set; }

        [DataMember]
        public string Title { get; set; }
    }
}
