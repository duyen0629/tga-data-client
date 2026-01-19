using System.Runtime.Serialization;

namespace TgaGateway2.Models
{
    /// <summary>
    /// Flattened release file record for saving to Supabase
    /// </summary>
    [DataContract]
    public class ReleaseFileRecord
    {
        [DataMember]
        public string ReleaseFileKey { get; set; }

        [DataMember]
        public string TrainingComponentCode { get; set; }

        [DataMember]
        public string ReleaseNumber { get; set; }

        [DataMember]
        public string ReleaseDate { get; set; }

        [DataMember]
        public string ReleaseCurrency { get; set; }

        [DataMember]
        public string RelativePath { get; set; }

        [DataMember]
        public int Size { get; set; }
    }
}
