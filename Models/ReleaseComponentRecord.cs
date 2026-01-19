using System.Runtime.Serialization;

namespace TgaGateway2.Models
{
    /// <summary>
    /// Flattened release component record for saving to Supabase
    /// </summary>
    [DataContract]
    public class ReleaseComponentRecord
    {
        [DataMember]
        public string ReleaseComponentKey { get; set; }

        [DataMember]
        public string TrainingComponentCode { get; set; }

        [DataMember]
        public string ReleaseNumber { get; set; }

        [DataMember]
        public string ReleaseDate { get; set; }

        [DataMember]
        public string ReleaseCurrency { get; set; }

        [DataMember]
        public string ComponentCode { get; set; }

        [DataMember]
        public string ComponentTitle { get; set; }

        [DataMember]
        public string ComponentType { get; set; }

        [DataMember]
        public string ComponentReleaseNumber { get; set; }

        [DataMember]
        public string ComponentReleaseDate { get; set; }

        [DataMember]
        public string ComponentReleaseCurrency { get; set; }
    }
}
