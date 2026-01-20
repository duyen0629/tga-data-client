using System.Runtime.Serialization;

namespace TgaGateway2.Models
{
    /// <summary>
    /// Flattened lookup record for saving to Supabase
    /// </summary>
    [DataContract]
    public class LookupRecord
    {
        [DataMember]
        public string LookupKey { get; set; }

        [DataMember]
        public string LookupName { get; set; }

        [DataMember]
        public string Code { get; set; }

        [DataMember]
        public string Description { get; set; }

        public ExtensionDataObject ExtensionData { get; set; }
    }
}
