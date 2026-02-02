using System.Runtime.Serialization;

namespace TgaGateway2.Models
{
    /// <summary>
    /// Merged training component document record (Complete + Assessment Requirements).
    /// </summary>
    [DataContract]
    public class TrainingComponentDocumentRecord
    {
        [DataMember]
        public string TrainingComponentCode { get; set; }

        [DataMember]
        public string ReleaseNumber { get; set; }

        [DataMember]
        public string Title { get; set; }

        [DataMember]
        public JsonRaw SourceFiles { get; set; }

        [DataMember]
        public JsonRaw ContentJson { get; set; }

        [DataMember]
        public string RawXml { get; set; }

        [DataMember]
        public string ParsedAt { get; set; }

        [DataMember]
        public string ProcessError { get; set; }
    }
}
