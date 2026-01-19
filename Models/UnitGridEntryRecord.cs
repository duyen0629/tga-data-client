using System.Runtime.Serialization;

namespace TgaGateway2.Models
{
    /// <summary>
    /// Flattened unit grid entry record for saving to Supabase
    /// </summary>
    [DataContract]
    public class UnitGridEntryRecord
    {
        [DataMember]
        public string UnitGridEntryKey { get; set; }

        [DataMember]
        public string TrainingComponentCode { get; set; }

        [DataMember]
        public string ReleaseNumber { get; set; }

        [DataMember]
        public string ReleaseDate { get; set; }

        [DataMember]
        public string ReleaseCurrency { get; set; }

        [DataMember]
        public string UnitCode { get; set; }

        [DataMember]
        public string UnitTitle { get; set; }
    }
}
