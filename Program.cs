using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using TgaGateway2.Handlers.TrainingComponentService;
using TgaGateway2.Services;

namespace TgaGateway2
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var runStart = DateTime.Now;
            var logBuilder = new StringBuilder();
            var originalOut = Console.Out;
            var originalErr = Console.Error;
            var teeWriter = new TeeTextWriter(originalOut, logBuilder);
            Console.SetOut(teeWriter);
            Console.SetError(teeWriter);

            try
            {
                await RunAllProcesses();
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR:");
                Console.WriteLine(ex.Message);
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                    if (ex.InnerException.InnerException != null)
                    {
                        Console.WriteLine($"Inner Inner Exception: {ex.InnerException.InnerException.Message}");
                    }
                }
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            }
            finally
            {
                try
                {
                    var logPath = SaveLogToFile(logBuilder.ToString(), runStart);
                    Console.SetOut(originalOut);
                    Console.SetError(originalErr);
                    Console.WriteLine($"Run log saved to: {logPath}");
                }
                catch (Exception logEx)
                {
                    Console.SetOut(originalOut);
                    Console.SetError(originalErr);
                    Console.WriteLine($"Failed to save run log: {logEx.Message}");
                }
            }

            Console.WriteLine();
            Console.Write("Press Enter to exit...");
            Console.ReadLine();
        }

        private static async Task RunAllProcesses()
        {
            // Initialize services
            using (var tgaService = new TgaDataService())
            using (var supabaseService = new SupabaseService())
            {
                Console.WriteLine("=== Training Component Service Getting Data ===\n");

                // 1. Get Server Time
                var serverTime = tgaService.GetServerTime();
                Console.WriteLine($"Server time: {serverTime}\n");

                // 2. Process ALL Recognition Managers (fetch, display, and save to Supabase)
                await ProcessRecognitionManagers(supabaseService);

                // 3. Process ALL Data Managers (fetch, display, and save to Supabase)
                await ProcessDataManagers(supabaseService);

                // 4. Process ALL Validation Codes (fetch, display, and save to Supabase)
                await ProcessValidationCodes(supabaseService);

                // 5. Process ALL Contact Roles (fetch, display, and save to Supabase)
                await ProcessContactRoles(supabaseService);

                // 6. Process ALL Address States (fetch, display, and save to Supabase)
                await ProcessAddressStates(supabaseService);

                // 7. Process Recognition Manager Assignments (via GetDetails)
                await ProcessRecognitionManagerAssignments(supabaseService);

                // 8. Process Data Manager Assignments (via GetDetails)
                await ProcessDataManagerAssignments(supabaseService);

                // Commented out as there is no data for releases in the database
                // 9. Process Releases (via GetDetails) 
                // await ProcessReleases(supabaseService);

                // Commented out as there is no data for release_files in the database
                // 10. Process Release Files (via GetDetails)
                // await ProcessReleaseFiles(supabaseService);

                // 11. Process Contacts (via GetDetails)
                await ProcessContacts(supabaseService);

                // 12. Process Classifications (via GetDetails)
                await ProcessClassifications(supabaseService);

                // 13. Process Mapping Information (via GetDetails)
                await ProcessMappings(supabaseService);

                // 14. Process Currency Periods (via GetDetails)
                await ProcessCurrencyPeriods(supabaseService);

                // 15. Process Usage Recommendations (via GetDetails)
                await ProcessUsageRecommendations(supabaseService);

                // 16. Process Completion Mappings (via GetDetails)
                await ProcessCompletionMappings(supabaseService);

                // 17. Process Training Component Summaries (fetch and save to Supabase)
                await ProcessTrainingComponentSummaries(supabaseService);
            }
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
                var recognitionManagerAssignments = await RecognitionManagerAssignmentHandler.ProcessRecognitionManagerAssignments(
                    summaryService,
                    assignmentService,
                    supabaseService,
                    startDate: new DateTime(2016, 1, 15), // 15/01/2016
                    endDate: new DateTime(2026, 1, 15),   // 15/01/2026
                    maxResults: 0); // 0 = fetch all via pagination
            }
        }

        private static async Task ProcessDataManagerAssignments(SupabaseService supabaseService)
        {
            using (var summaryService = new TrainingComponentSummaryService())
            using (var assignmentService = new DataManagerAssignmentService())
            {
                var dataManagerAssignments = await DataManagerAssignmentHandler.ProcessDataManagerAssignments(
                    summaryService,
                    assignmentService,
                    supabaseService,
                    startDate: new DateTime(2016, 1, 15), // 15/01/2016
                    endDate: new DateTime(2026, 1, 15),   // 15/01/2026
                    maxResults: 0); // 0 = fetch all via pagination
            }
        }

        private static async Task ProcessReleases(SupabaseService supabaseService)
        {
            using (var summaryService = new TrainingComponentSummaryService())
            using (var releaseService = new ReleaseService())
            {
                var releases = await ReleaseHandler.ProcessReleases(
                    summaryService,
                    releaseService,
                    supabaseService,
                    startDate: new DateTime(2016, 1, 15), // 15/01/2016
                    endDate: new DateTime(2026, 1, 15),   // 15/01/2026
                    maxResults: 0); // 0 = fetch all via pagination
            }
        }

        private static async Task ProcessContacts(SupabaseService supabaseService)
        {
            using (var summaryService = new TrainingComponentSummaryService())
            using (var contactService = new ContactService())
            {
                var contacts = await ContactHandler.ProcessContacts(
                    summaryService,
                    contactService,
                    supabaseService,
                    startDate: new DateTime(2016, 1, 15), // 15/01/2016
                    endDate: new DateTime(2026, 1, 15),   // 15/01/2026
                    maxResults: 0); // 0 = fetch all via pagination
            }
        }

        private static async Task ProcessClassifications(SupabaseService supabaseService)
        {
            using (var summaryService = new TrainingComponentSummaryService())
            using (var classificationService = new ClassificationService())
            {
                var classifications = await ClassificationHandler.ProcessClassifications(
                    summaryService,
                    classificationService,
                    supabaseService,
                    startDate: new DateTime(2016, 1, 15), // 15/01/2016
                    endDate: new DateTime(2026, 1, 15),   // 15/01/2026
                    maxResults: 0); // 0 = fetch all via pagination
            }
        }

        private static async Task ProcessMappings(SupabaseService supabaseService)
        {
            using (var summaryService = new TrainingComponentSummaryService())
            using (var mappingService = new MappingService())
            {
                var mappings = await MappingHandler.ProcessMappings(
                    summaryService,
                    mappingService,
                    supabaseService,
                    startDate: new DateTime(2016, 1, 15), // 15/01/2016
                    endDate: new DateTime(2026, 1, 15),   // 15/01/2026
                    maxResults: 0); // 0 = fetch all via pagination
            }
        }

        private static async Task ProcessCurrencyPeriods(SupabaseService supabaseService)
        {
            using (var summaryService = new TrainingComponentSummaryService())
            using (var currencyPeriodService = new CurrencyPeriodService())
            {
                var currencyPeriods = await CurrencyPeriodHandler.ProcessCurrencyPeriods(
                    summaryService,
                    currencyPeriodService,
                    supabaseService,
                    startDate: new DateTime(2016, 1, 15), // 15/01/2016
                    endDate: new DateTime(2026, 1, 15),   // 15/01/2026
                    maxResults: 0); // 0 = fetch all via pagination
            }
        }

        private static async Task ProcessUsageRecommendations(SupabaseService supabaseService)
        {
            using (var summaryService = new TrainingComponentSummaryService())
            using (var usageRecommendationService = new UsageRecommendationService())
            {
                var usageRecommendations = await UsageRecommendationHandler.ProcessUsageRecommendations(
                    summaryService,
                    usageRecommendationService,
                    supabaseService,
                    startDate: new DateTime(2016, 1, 15), // 15/01/2016
                    endDate: new DateTime(2026, 1, 15),   // 15/01/2026
                    maxResults: 0); // 0 = fetch all via pagination
            }
        }

        private static async Task ProcessCompletionMappings(SupabaseService supabaseService)
        {
            using (var summaryService = new TrainingComponentSummaryService())
            using (var completionMappingService = new CompletionMappingService())
            {
                var completionMappings = await CompletionMappingHandler.ProcessCompletionMappings(
                    summaryService,
                    completionMappingService,
                    supabaseService,
                    startDate: new DateTime(2016, 1, 15), // 15/01/2016
                    endDate: new DateTime(2026, 1, 15),   // 15/01/2026
                    maxResults: 0); // 0 = fetch all via pagination
            }
        }

        private static async Task ProcessReleaseFiles(SupabaseService supabaseService)
        {
            using (var summaryService = new TrainingComponentSummaryService())
            using (var releaseService = new ReleaseService())
            {
                var releaseFiles = await ReleaseFileHandler.ProcessReleaseFiles(
                    summaryService,
                    releaseService,
                    supabaseService,
                    startDate: new DateTime(2016, 1, 15), // 15/01/2016
                    endDate: new DateTime(2026, 1, 15),   // 15/01/2026
                    maxResults: 0); // 0 = fetch all via pagination
            }
        }

        private static async Task ProcessTrainingComponentSummaries(SupabaseService supabaseService)
        {
            using (var summaryService = new TrainingComponentSummaryService())
            {
                var trainingComponentSummaries = await TrainingComponentSummaryHandler.ProcessTrainingComponentSummaries(
                    summaryService,
                    supabaseService,
                    startDate: new DateTime(2016, 1, 15), // 15/01/2016
                    endDate: new DateTime(2026, 1, 15),   // 15/01/2026
                    maxResults: 0); // 0 = fetch all via pagination
            }
        }

        private static string SaveLogToFile(string logContent, DateTime runStart)
        {
            var logsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            Directory.CreateDirectory(logsDirectory);

            var fileName = $"run-{runStart:yyyyMMdd-HHmmss}.txt";
            var filePath = Path.Combine(logsDirectory, fileName);
            File.WriteAllText(filePath, logContent, new UTF8Encoding(false));

            return filePath;
        }

        private sealed class TeeTextWriter : TextWriter
        {
            private readonly TextWriter _consoleWriter;
            private readonly StringBuilder _buffer;
            private readonly object _lock = new object();

            public TeeTextWriter(TextWriter consoleWriter, StringBuilder buffer)
            {
                _consoleWriter = consoleWriter ?? throw new ArgumentNullException(nameof(consoleWriter));
                _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            }

            public override Encoding Encoding => _consoleWriter.Encoding;

            public override void Write(char value)
            {
                lock (_lock)
                {
                    _buffer.Append(value);
                    _consoleWriter.Write(value);
                }
            }

            public override void Write(string value)
            {
                lock (_lock)
                {
                    _buffer.Append(value);
                    _consoleWriter.Write(value);
                }
            }

            public override void WriteLine(string value)
            {
                lock (_lock)
                {
                    _buffer.AppendLine(value);
                    _consoleWriter.WriteLine(value);
                }
            }
        }
    }
}
