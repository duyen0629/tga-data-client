using System.Runtime.Serialization;

namespace training.gov.au.services
{
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Runtime.Serialization", "4.0.0.0")]
    [DataContractAttribute(Name = "RtoClassificationSchemeResult", Namespace = "http://training.gov.au/services/")]
    public partial class RtoClassificationSchemeResult : System.Runtime.Serialization.IExtensibleDataObject
    {
        private ExtensionDataObject extensionDataField;
        private bool AllowMultipleValuesField;
        private ClassificationValue[] ClassificationValuesField;
        private string DescriptionField;
        private bool IsProtectedField;
        private bool IsRequiredField;
        private string NameField;
        private string SchemeCodeField;

        public ExtensionDataObject ExtensionData
        {
            get { return this.extensionDataField; }
            set { this.extensionDataField = value; }
        }

        [DataMemberAttribute()]
        public bool AllowMultipleValues
        {
            get { return this.AllowMultipleValuesField; }
            set { this.AllowMultipleValuesField = value; }
        }

        [DataMemberAttribute(IsRequired = true)]
        public ClassificationValue[] ClassificationValues
        {
            get { return this.ClassificationValuesField; }
            set { this.ClassificationValuesField = value; }
        }

        [DataMemberAttribute(IsRequired = true)]
        public string Description
        {
            get { return this.DescriptionField; }
            set { this.DescriptionField = value; }
        }

        [DataMemberAttribute()]
        public bool IsProtected
        {
            get { return this.IsProtectedField; }
            set { this.IsProtectedField = value; }
        }

        [DataMemberAttribute()]
        public bool IsRequired
        {
            get { return this.IsRequiredField; }
            set { this.IsRequiredField = value; }
        }

        [DataMemberAttribute(IsRequired = true)]
        public string Name
        {
            get { return this.NameField; }
            set { this.NameField = value; }
        }

        [DataMemberAttribute(IsRequired = true)]
        public string SchemeCode
        {
            get { return this.SchemeCodeField; }
            set { this.SchemeCodeField = value; }
        }
    }
}
