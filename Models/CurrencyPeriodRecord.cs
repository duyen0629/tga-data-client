using System.Runtime.Serialization;

namespace TgaGateway2.Models
{
    /// <summary>
    /// Flattened currency period record for saving to Supabase
    /// </summary>
    [DataContract]
    public class CurrencyPeriodRecord
    {
        [DataMember]
        public string CurrencyPeriodKey { get; set; }

        [DataMember]
        public string TrainingComponentCode { get; set; }

        [DataMember]
        public string Authority { get; set; }

        [DataMember]
        public string EndComment { get; set; }

        [DataMember]
        public string EndReasonCode { get; set; }

        [DataMember]
        public string ActionOnEntity { get; set; }

        [DataMember]
        public string StartDate { get; set; }

        [DataMember]
        public string EndDate { get; set; }
    }
}
