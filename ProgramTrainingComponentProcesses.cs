using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using training.gov.au.services;
using TgaGateway2.Handlers.TrainingComponentDocuments;
using TgaGateway2.Handlers.TrainingComponentService;
using TgaGateway2.Services;

namespace TgaGateway2
{
    internal partial class Program
    {
        /// <summary>
        /// Default TGA modified-date search window: from ten years ago through the current time (<c>DateTime.Now</c> for both bounds in one call).
        /// </summary>
        public static (DateTime startDate, DateTime endDate) GetTgaDefaultModifiedDateRange()
        {
            var end = DateTime.Now;
            return (end.AddYears(-10), end);
        }

        private static async Task ProcessRecognitionManagers(SupabaseService supabaseService)
        {
            using (var recognitionManagerService = new RecognitionManagerService())
            {
                var recognitionManagers = await RecognitionManagerHandler.ProcessRecognitionManagers(
                    recognitionManagerService,
                    supabaseService);
            }
        }

        private static async Task ProcessDataManagers(SupabaseService supabaseService)
        {
            using (var dataManagerService = new DataManagerService())
            {
                var dataManagers = await DataManagerHandler.ProcessDataManagers(
                    dataManagerService,
                    supabaseService);
            }
        }

        private static async Task ProcessValidationCodes(SupabaseService supabaseService)
        {
            using (var validationCodeService = new ValidationCodeService())
            {
                var validationCodes = await ValidationCodeHandler.ProcessValidationCodes(
                    validationCodeService,
                    supabaseService);
            }
        }

        private static async Task ProcessClassificationSchemes(SupabaseService supabaseService)
        {
            using (var schemeService = new ClassificationSchemeService())
            {
                var schemes = await ClassificationSchemeHandler.ProcessClassificationSchemes(
                    schemeService,
                    supabaseService);
            }
        }

        private static async Task ProcessClassificationPurposes(SupabaseService supabaseService)
        {
            using (var purposeService = new ClassificationPurposeService())
            {
                var purposes = await ClassificationPurposeHandler.ProcessClassificationPurposes(
                    purposeService,
                    supabaseService);
            }
        }

        private static async Task ProcessLookups(SupabaseService supabaseService)
        {
            using (var lookupService = new LookupService())
            {
                var lookups = await LookupHandler.ProcessLookups(
                    lookupService,
                    supabaseService);
            }
        }

        private static async Task ProcessContactRoles(SupabaseService supabaseService)
        {
            using (var contactRoleService = new ContactRoleService())
            {
                var contactRoles = await ContactRoleHandler.ProcessContactRoles(
                    contactRoleService,
                    supabaseService);
            }
        }

        private static async Task ProcessAddressStates(SupabaseService supabaseService)
        {
            using (var addressStateService = new AddressStateService())
            {
                var addressStates = await AddressStateHandler.ProcessAddressStates(
                    addressStateService,
                    supabaseService);
            }
        }

        private static async Task ProcessRecognitionManagerAssignments(SupabaseService supabaseService)
        {
            using (var summaryService = new TrainingComponentSummaryService())
            using (var assignmentService = new RecognitionManagerAssignmentService())
            {
                var (startDate, endDate) = GetTgaDefaultModifiedDateRange();
                var recognitionManagerAssignments = await RecognitionManagerAssignmentHandler.ProcessRecognitionManagerAssignments(
                    summaryService,
                    assignmentService,
                    supabaseService,
                    startDate: startDate,
                    endDate: endDate,
                    maxResults: 0); // 0 = fetch all via pagination
            }
        }

        private static async Task ProcessDataManagerAssignments(SupabaseService supabaseService)
        {
            using (var summaryService = new TrainingComponentSummaryService())
            using (var assignmentService = new DataManagerAssignmentService())
            {
                var (startDate, endDate) = GetTgaDefaultModifiedDateRange();
                var dataManagerAssignments = await DataManagerAssignmentHandler.ProcessDataManagerAssignments(
                    summaryService,
                    assignmentService,
                    supabaseService,
                    startDate: startDate,
                    endDate: endDate,
                    maxResults: 0); // 0 = fetch all via pagination
            }
        }

        private static async Task ProcessReleases(SupabaseService supabaseService)
        {
            using (var summaryService = new TrainingComponentSummaryService())
            using (var releaseService = new ReleaseService())
            {
                var (startDate, endDate) = GetTgaDefaultModifiedDateRange();
                var releases = await ReleaseHandler.ProcessReleases(
                    summaryService,
                    releaseService,
                    supabaseService,
                    startDate: startDate,
                    endDate: endDate,
                    maxResults: 0); // 0 = fetch all via pagination
            }
        }

        private static async Task ProcessContacts(SupabaseService supabaseService)
        {
            using (var summaryService = new TrainingComponentSummaryService())
            using (var contactService = new ContactService())
            {
                var (startDate, endDate) = GetTgaDefaultModifiedDateRange();
                var contacts = await ContactHandler.ProcessContacts(
                    summaryService,
                    contactService,
                    supabaseService,
                    startDate: startDate,
                    endDate: endDate,
                    maxResults: 0); // 0 = fetch all via pagination
            }
        }

        private static async Task ProcessClassifications(SupabaseService supabaseService)
        {
            using (var summaryService = new TrainingComponentSummaryService())
            using (var classificationService = new ClassificationService())
            {
                var (startDate, endDate) = GetTgaDefaultModifiedDateRange();
                var classifications = await ClassificationHandler.ProcessClassifications(
                    summaryService,
                    classificationService,
                    supabaseService,
                    startDate: startDate,
                    endDate: endDate,
                    maxResults: 0); // 0 = fetch all via pagination
            }
        }

        private static async Task ProcessMappings(SupabaseService supabaseService)
        {
            using (var summaryService = new TrainingComponentSummaryService())
            using (var mappingService = new MappingService())
            {
                var (startDate, endDate) = GetTgaDefaultModifiedDateRange();
                var mappings = await MappingHandler.ProcessMappings(
                    summaryService,
                    mappingService,
                    supabaseService,
                    startDate: startDate,
                    endDate: endDate,
                    maxResults: 0); // 0 = fetch all via pagination
            }
        }

        private static async Task ProcessCurrencyPeriods(SupabaseService supabaseService)
        {
            using (var summaryService = new TrainingComponentSummaryService())
            using (var currencyPeriodService = new CurrencyPeriodService())
            {
                var (startDate, endDate) = GetTgaDefaultModifiedDateRange();
                var currencyPeriods = await CurrencyPeriodHandler.ProcessCurrencyPeriods(
                    summaryService,
                    currencyPeriodService,
                    supabaseService,
                    startDate: startDate,
                    endDate: endDate,
                    maxResults: 0); // 0 = fetch all via pagination
            }
        }

        private static async Task ProcessUsageRecommendations(SupabaseService supabaseService)
        {
            using (var summaryService = new TrainingComponentSummaryService())
            using (var usageRecommendationService = new UsageRecommendationService())
            {
                var (startDate, endDate) = GetTgaDefaultModifiedDateRange();
                var usageRecommendations = await UsageRecommendationHandler.ProcessUsageRecommendations(
                    summaryService,
                    usageRecommendationService,
                    supabaseService,
                    startDate: startDate,
                    endDate: endDate,
                    maxResults: 0); // 0 = fetch all via pagination
            }
        }

        private static async Task ProcessCompletionMappings(SupabaseService supabaseService)
        {
            using (var summaryService = new TrainingComponentSummaryService())
            using (var completionMappingService = new CompletionMappingService())
            {
                var (startDate, endDate) = GetTgaDefaultModifiedDateRange();
                var completionMappings = await CompletionMappingHandler.ProcessCompletionMappings(
                    summaryService,
                    completionMappingService,
                    supabaseService,
                    startDate: startDate,
                    endDate: endDate,
                    maxResults: 0); // 0 = fetch all via pagination
            }
        }

        private static async Task ProcessReleaseFiles(SupabaseService supabaseService)
        {
            using (var summaryService = new TrainingComponentSummaryService())
            using (var releaseService = new ReleaseService())
            {
                var (startDate, endDate) = GetTgaDefaultModifiedDateRange();
                var releaseFiles = await ReleaseFileHandler.ProcessReleaseFiles(
                    summaryService,
                    releaseService,
                    supabaseService,
                    startDate: startDate,
                    endDate: endDate,
                    maxResults: 0); // 0 = fetch all via pagination
            }
        }

        private static async Task GetTrainingComponentSummaryForCode(string trainingComponentCode)
        {
            // Try Supabase first
            var queryService = new SupabaseQueryService();
            var summary = await queryService.GetTrainingComponentSummaryByCode(trainingComponentCode);
            if (summary != null)
            {
                Console.WriteLine($"=== training_component_summary for {trainingComponentCode} (from Supabase) ===");
                foreach (var kvp in summary)
                {
                    var value = kvp.Value?.ToString() ?? "(null)";
                    if (value.Length > 100) value = value.Substring(0, 97) + "...";
                    Console.WriteLine($"  {kvp.Key}: {value}");
                }
                return;
            }

            // Fallback: fetch from TGA API via GetDetails
            Console.WriteLine($"  Not in Supabase. Fetching from TGA API (GetDetails)...");
            using (var summaryService = new TrainingComponentSummaryService())
            {
                var details = summaryService.GetDetailsByCode(trainingComponentCode);
                if (details == null)
                {
                    Console.WriteLine($"No training_component_summary found for code: {trainingComponentCode}");
                    return;
                }
                Console.WriteLine($"=== training_component_summary for {trainingComponentCode} (from TGA API) ===");
                Console.WriteLine($"  code: {details.Code}");
                Console.WriteLine($"  title: {details.Title}");
                Console.WriteLine($"  component_type: {details.ComponentType}");
                Console.WriteLine($"  is_confidential: {details.IsConfidential}");
                Console.WriteLine($"  created_date: {details.CreatedDate}");
                Console.WriteLine($"  updated_date: {details.UpdatedDate}");
                if (details.UsageRecommendations != null && details.UsageRecommendations.Length > 0)
                {
                    var ur = details.UsageRecommendations[0];
                    Console.WriteLine($"  usage_recommendation: {ur?.State}");
                    Console.WriteLine($"  start_date: {ur?.StartDate}");
                }
                if (details is TrainingComponent2 tc2)
                    Console.WriteLine($"  is_legacy_data: {tc2.IsLegacyData}");
            }
        }

        private static async Task ProcessTrainingComponentDocumentForCode(SupabaseService supabaseService, string trainingComponentCode)
        {
            await TrainingComponentDocumentHandler.ProcessTrainingComponentDocumentForCode(
                supabaseService,
                trainingComponentCode);
        }

        private static async Task RunTrainingComponentDocumentProcess(SupabaseService supabaseService)
        {
            await TrainingComponentDocumentHandler.ProcessTrainingComponentDocumentsForAll(supabaseService, 0, batchSize: 1000);
        }

        private static async Task ProcessReleaseComponents(SupabaseService supabaseService)
        {
            using (var summaryService = new TrainingComponentSummaryService())
            using (var releaseService = new ReleaseService())
            {
                var (startDate, endDate) = GetTgaDefaultModifiedDateRange();
                var releaseComponents = await ReleaseComponentHandler.ProcessReleaseComponents(
                    summaryService,
                    releaseService,
                    supabaseService,
                    startDate: startDate,
                    endDate: endDate,
                    maxResults: 0); // 0 = fetch all via pagination
            }
        }

        private static async Task ProcessUnitGridEntries(SupabaseService supabaseService)
        {
            using (var summaryService = new TrainingComponentSummaryService())
            using (var releaseService = new ReleaseService())
            {
                var (startDate, endDate) = GetTgaDefaultModifiedDateRange();
                var unitGridEntries = await UnitGridEntryHandler.ProcessUnitGridEntries(
                    summaryService,
                    releaseService,
                    supabaseService,
                    startDate: startDate,
                    endDate: endDate,
                    maxResults: 0); // 0 = fetch all via pagination
            }
        }

        private static async Task ProcessTrainingComponentSummaries(SupabaseService supabaseService)
        {
            using (var summaryService = new TrainingComponentSummaryService())
            {
                var (startDate, endDate) = GetTgaDefaultModifiedDateRange();
                var trainingComponentSummaries = await TrainingComponentSummaryHandler.ProcessTrainingComponentSummaries(
                    summaryService,
                    supabaseService,
                    startDate: startDate,
                    endDate: endDate,
                    maxResults: 0); // 0 = fetch all via pagination
            }
        }

        private static async Task ProcessDeletedTrainingComponents(SupabaseService supabaseService)
        {
            using (var deletedService = new DeletedTrainingComponentService())
            {
                var (startDate, endDate) = GetTgaDefaultModifiedDateRange();
                var deletedTrainingComponents = await DeletedTrainingComponentHandler.ProcessDeletedTrainingComponents(
                    deletedService,
                    supabaseService,
                    startDate: startDate,
                    endDate: endDate,
                    maxResults: 0,                        // 0 = fetch all via pagination
                    pageSize: 500);
            }
        }
    }
}
