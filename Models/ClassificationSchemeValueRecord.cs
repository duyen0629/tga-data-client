using System.Runtime.Serialization;

namespace TgaGateway2.Models
{
    /// <summary>
    /// Flattened classification scheme value record for saving to Supabase
    /// </summary>
    [DataContract]
    public class ClassificationSchemeValueRecord
    {
        [DataMember]
        public string ClassificationValueKey { get; set; }

        [DataMember]
        public string SchemeCode { get; set; }

        [DataMember]
        public string Value { get; set; }

        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public string Description { get; set; }

        [DataMember]
        public int DisplayOrder { get; set; }

        [DataMember]
        public string ActionOnEntity { get; set; }

        [DataMember]
        public string StartDate { get; set; }

        [DataMember]
        public string EndDate { get; set; }

        public ExtensionDataObject ExtensionData { get; set; }
    }
}
