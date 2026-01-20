using System;
using System.Runtime.Serialization;
using System.ServiceModel;
using training.gov.au.services;

[ServiceContract(Namespace = "http://training.gov.au/services/", ConfigurationName = "IOrganisationService")]
public interface IOrganisationService
{
    [OperationContract(Action = "http://training.gov.au/services/IOrganisationService/SearchByModifiedDate", ReplyAction = "http://training.gov.au/services/IOrganisationService/SearchByModifiedDateResponse")]
    [FaultContract(typeof(ValidationFault), Action = "http://training.gov.au/services/IOrganisationService/SearchByModifiedDateValidationFaultFault", Name = "ValidationFault")]
    OrganisationSearchResult SearchByModifiedDate(OrganisationModifiedSearchRequest request);
}

namespace training.gov.au.services
{
    [DataContract(Name = "OrganisationModifiedSearchRequest", Namespace = "http://training.gov.au/services/")]
    public class OrganisationModifiedSearchRequest : IExtensibleDataObject
    {
        private ExtensionDataObject _extensionData;

        [DataMember]
        public string[] DataManagerFilter { get; set; }

        [DataMember]
        public System.DateTimeOffset? EndDate { get; set; }

        [DataMember]
        public System.DateTimeOffset? StartDate { get; set; }

        [DataMember]
        public int PageNumber { get; set; }

        [DataMember]
        public int PageSize { get; set; }

        public ExtensionDataObject ExtensionData
        {
            get => _extensionData;
            set => _extensionData = value;
        }
    }

    [DataContract(Name = "OrganisationSearchResult", Namespace = "http://training.gov.au/services/")]
    public class OrganisationSearchResult : IExtensibleDataObject
    {
        private ExtensionDataObject _extensionData;

        [DataMember]
        public int Count { get; set; }

        [DataMember]
        public int PageNumber { get; set; }

        [DataMember]
        public int PageSize { get; set; }

        [DataMember]
        public OrganisationSearchResultItem[] Results { get; set; }

        public ExtensionDataObject ExtensionData
        {
            get => _extensionData;
            set => _extensionData = value;
        }
    }

    [DataContract(Name = "OrganisationSearchResultItem", Namespace = "http://training.gov.au/services/")]
    [KnownType(typeof(OrganisationSearchResultItem2))]
    [KnownType(typeof(OrganisationSearchResultItem3))]
    public class OrganisationSearchResultItem : IExtensibleDataObject
    {
        private ExtensionDataObject _extensionData;

        [DataMember]
        public string Code { get; set; }

        [DataMember]
        public string DataManagerCode { get; set; }

        [DataMember]
        public bool HasActiveRegistration { get; set; }

        [DataMember]
        public string LegalPersonName { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public string TradingName { get; set; }

        [DataMember]
        public System.DateTimeOffset UpdatedDate { get; set; }

        public ExtensionDataObject ExtensionData
        {
            get => _extensionData;
            set => _extensionData = value;
        }
    }

    [DataContract(Name = "OrganisationSearchResultItem2", Namespace = "http://training.gov.au/services/")]
    [KnownType(typeof(OrganisationSearchResultItem3))]
    public class OrganisationSearchResultItem2 : OrganisationSearchResultItem
    {
        [DataMember]
        public bool IsLegacyData { get; set; }
    }

    [DataContract(Name = "OrganisationSearchResultItem3", Namespace = "http://training.gov.au/services/")]
    public class OrganisationSearchResultItem3 : OrganisationSearchResultItem2
    {
        [DataMember]
        public string RegistrationStatus { get; set; }
    }
}
