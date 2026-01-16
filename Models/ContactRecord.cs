using System.Runtime.Serialization;

namespace TgaGateway2.Models
{
    /// <summary>
    /// Flattened contact record for saving to Supabase
    /// </summary>
    [DataContract]
    public class ContactRecord
    {
        [DataMember]
        public string ContactKey { get; set; }

        [DataMember]
        public string TrainingComponentCode { get; set; }

        [DataMember]
        public string RoleCode { get; set; }

        [DataMember]
        public string TypeCode { get; set; }

        [DataMember]
        public string FirstName { get; set; }

        [DataMember]
        public string LastName { get; set; }

        [DataMember]
        public string OrganisationName { get; set; }

        [DataMember]
        public string Email { get; set; }

        [DataMember]
        public string Phone { get; set; }

        [DataMember]
        public string Mobile { get; set; }

        [DataMember]
        public string Fax { get; set; }

        [DataMember]
        public string GroupName { get; set; }

        [DataMember]
        public string JobTitle { get; set; }

        [DataMember]
        public string Title { get; set; }

        [DataMember]
        public string PostalCountryCode { get; set; }

        [DataMember]
        public string PostalLine1 { get; set; }

        [DataMember]
        public string PostalLine2 { get; set; }

        [DataMember]
        public string PostalSuburb { get; set; }

        [DataMember]
        public string PostalStateCode { get; set; }

        [DataMember]
        public string PostalStateOverseas { get; set; }

        [DataMember]
        public string PostalPostcode { get; set; }

        [DataMember]
        public string ActionOnEntity { get; set; }

        [DataMember]
        public string StartDate { get; set; }

        [DataMember]
        public string EndDate { get; set; }
    }
}
