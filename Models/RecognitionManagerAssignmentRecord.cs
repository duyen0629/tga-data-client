using System.Runtime.Serialization;

namespace TgaGateway2.Models
{
    /// <summary>
    /// Flattened recognition manager assignment record for saving to Supabase
    /// </summary>
    [DataContract]
    public class RecognitionManagerAssignmentRecord
    {
        [DataMember]
        public string TrainingComponentCode { get; set; }

        [DataMember]
        public string RecognitionManagerCode { get; set; }

        [DataMember]
        public string ActionOnEntity { get; set; }

        [DataMember]
        public string StartDate { get; set; }

        [DataMember]
        public string EndDate { get; set; }
    }
}
