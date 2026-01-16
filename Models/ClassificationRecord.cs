using System.Runtime.Serialization;

namespace TgaGateway2.Models
{
    /// <summary>
    /// Flattened classification record for saving to Supabase
    /// </summary>
    [DataContract]
    public class ClassificationRecord
    {
        [DataMember]
        public string ClassificationKey { get; set; }

        [DataMember]
        public string TrainingComponentCode { get; set; }

        [DataMember]
        public string PurposeCode { get; set; }

        [DataMember]
        public string SchemeCode { get; set; }

        [DataMember]
        public string ValueCode { get; set; }

        [DataMember]
        public string ActionOnEntity { get; set; }

        [DataMember]
        public string StartDate { get; set; }

        [DataMember]
        public string EndDate { get; set; }
    }
}
