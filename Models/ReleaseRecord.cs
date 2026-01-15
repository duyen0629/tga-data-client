using System.Runtime.Serialization;

namespace TgaGateway2.Models
{
    /// <summary>
    /// Flattened release record for saving to Supabase
    /// </summary>
    [DataContract]
    public class ReleaseRecord
    {
        [DataMember]
        public string TrainingComponentCode { get; set; }

        [DataMember]
        public string ReleaseNumber { get; set; }

        [DataMember]
        public string ReleaseDate { get; set; }

        [DataMember]
        public string Currency { get; set; }

        [DataMember]
        public string ApprovalProcess { get; set; }

        [DataMember]
        public string IscApprovalDate { get; set; }

        [DataMember]
        public string MinisterialAgreementDate { get; set; }

        [DataMember]
        public string NqcEndorsementDate { get; set; }

        [DataMember]
        public int ComponentsCount { get; set; }

        [DataMember]
        public int FilesCount { get; set; }

        [DataMember]
        public int UnitGridCount { get; set; }
    }
}
