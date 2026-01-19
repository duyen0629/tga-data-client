using System.Runtime.Serialization;

namespace TgaGateway2.Models
{
    /// <summary>
    /// Flattened completion mapping record for saving to Supabase
    /// </summary>
    [DataContract]
    public class CompletionMappingRecord
    {
        [DataMember]
        public string CompletionMappingKey { get; set; }

        [DataMember]
        public string TrainingComponentCode { get; set; }

        [DataMember]
        public string Code { get; set; }

        [DataMember]
        public bool IsMandatory { get; set; }

        [DataMember]
        public string ActionOnEntity { get; set; }

        [DataMember]
        public string StartDate { get; set; }

        [DataMember]
        public string EndDate { get; set; }
    }
}
