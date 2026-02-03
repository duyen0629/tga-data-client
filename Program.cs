using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using TgaGateway2.Handlers.TrainingComponentService;
using TgaGateway2.Services;

namespace TgaGateway2
{
    internal partial class Program
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
                //-----------Training Component Service -----------
                // await RunTrainingComponentServiceProcesses(tgaService, supabaseService);

                //-----------Training Component Documents -----------
                await RunTrainingComponentDocumentProcess(supabaseService);

                //-----------Organisation Service -----------
                // await RunOrganisationServiceProcesses(supabaseService);

                //-----------Classification Service -----------
                // await RunClassificationServiceProcesses(supabaseService);

                //-----------CSV Training Code Check -----------
                // var csvPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "RTOScopeExport.csv"));
                // var csvLogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "csv-training-code-check.txt");
                // await CSVTrainingCodeCheck.RunAsync(supabaseService, csvPath, "training_code", hasHeader: true, processMissing: true, logPath: csvLogPath);

                //-----------CSV Export Latest Documents -----------
                // var latestDocsCsvPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "training-component-documents-latest.csv");
                // await CSVTrainingCodeCheck.ExportLatestDocumentsToCsvAsync(supabaseService, csvPath, latestDocsCsvPath, "training_code", hasHeader: true);
            }
        }

        private static async Task RunClassificationServiceProcesses(SupabaseService supabaseService)
        {
            Console.WriteLine("=== Classification Service Getting Data ===\n");

            // 21. Process Classification Schemes (Classification Service)
            await ProcessClassificationSchemesFromClassificationService(supabaseService);
        }

        private static async Task RunTrainingComponentServiceProcesses(
            TgaDataService tgaService,
            SupabaseService supabaseService)
        {

            Console.WriteLine("=== Training Component Service Getting Data ===\n");

            // 1.Get Server Time
            var serverTime = tgaService.GetServerTime();
            Console.WriteLine($"Server time: {serverTime}\n");

            // 2. Process ALL Recognition Managers (fetch, display, and save to Supabase)
            await ProcessRecognitionManagers(supabaseService);

            // 3. Process ALL Data Managers (fetch, display, and save to Supabase)
            await ProcessDataManagers(supabaseService);

            // 4. Process ALL Validation Codes (fetch, display, and save to Supabase)
            await ProcessValidationCodes(supabaseService);

            // 4.1 Process ALL Classification Schemes (fetch, display, and save to Supabase)
            await ProcessClassificationSchemes(supabaseService);

            // 4.2 Process ALL Classification Purposes (fetch, display, and save to Supabase)
            await ProcessClassificationPurposes(supabaseService);

            // 4.3 Process ALL Lookups (fetch, display, and save to Supabase)
            await ProcessLookups(supabaseService);

            // 5. Process ALL Contact Roles (fetch, display, and save to Supabase)
            await ProcessContactRoles(supabaseService);

            // 6. Process ALL Address States (fetch, display, and save to Supabase)
            await ProcessAddressStates(supabaseService);

            // 7. Process Recognition Manager Assignments (via GetDetails)
            await ProcessRecognitionManagerAssignments(supabaseService);

            // 8. Process Data Manager Assignments (via GetDetails)
            await ProcessDataManagerAssignments(supabaseService);

            // 9.Process Releases(via GetDetails)
            await ProcessReleases(supabaseService);

            // 10.Process Release Files(via GetDetails)
            await ProcessReleaseFiles(supabaseService);

            // 11.Process Release Components(via GetDetails)
            await ProcessReleaseComponents(supabaseService);

            // 12.Process Unit Grid Entries(via GetDetails), UnitGridEntries live inside Release.UnitGrid
            await ProcessUnitGridEntries(supabaseService);

            // 13. Process Contacts (via GetDetails)
            await ProcessContacts(supabaseService);

            // 14. Process Classifications (via GetDetails)
            await ProcessClassifications(supabaseService);

            // 15. Process Mapping Information (via GetDetails)
            await ProcessMappings(supabaseService);

            // 16. Process Currency Periods (via GetDetails)
            await ProcessCurrencyPeriods(supabaseService);

            // 17. Process Usage Recommendations (via GetDetails)
            await ProcessUsageRecommendations(supabaseService);

            // 18. Process Completion Mappings (via GetDetails)
            await ProcessCompletionMappings(supabaseService);

            // 19. Process Training Component Summaries (fetch and save to Supabase)
            await ProcessTrainingComponentSummaries(supabaseService);

            // Commented out as there is no data for release_files in the database
            // 20. Process Deleted Training Components (via SearchDeletedByDeletedDate)
            // await ProcessDeletedTrainingComponents(supabaseService);
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
