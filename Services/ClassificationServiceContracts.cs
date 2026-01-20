using System.ServiceModel;
using training.gov.au.services;

[ServiceContract(Namespace = "http://training.gov.au/services/", ConfigurationName = "IClassificationService")]
public interface IClassificationService
{
    [OperationContract(Action = "http://training.gov.au/services/IClassificationService/SearchRtoClassificationsByScheme", ReplyAction = "http://training.gov.au/services/IClassificationService/SearchRtoClassificationsBySchemeResponse")]
    [FaultContract(typeof(ValidationFault), Action = "http://training.gov.au/services/IClassificationService/SearchRtoClassificationsBySchemeValidationFaultFault", Name = "ValidationFault")]
    RtoClassificationSchemeResult SearchRtoClassificationsByScheme(string SchemeCode);

    [OperationContract(Action = "http://training.gov.au/services/IClassificationService/SearchNrtClassificationsByScheme", ReplyAction = "http://training.gov.au/services/IClassificationService/SearchNrtClassificationsBySchemeResponse")]
    [FaultContract(typeof(ValidationFault), Action = "http://training.gov.au/services/IClassificationService/SearchNrtClassificationsBySchemeValidationFaultFault", Name = "ValidationFault")]
    NrtClassificationSchemeResult SearchNrtClassificationsByScheme(string SchemeCode);
}
