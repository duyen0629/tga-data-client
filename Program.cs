using System;
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
                // Initialize services
                using (var tgaService = new TgaDataService())
                using (var supabaseService = new SupabaseService())
                {
                    Console.WriteLine("=== Training Component Service Demo ===\n");

                    // 1. Get Server Time
                    var serverTime = tgaService.GetServerTime();
                    Console.WriteLine($"Server time: {serverTime}\n");

                    // // 2. Process ALL Recognition Managers (fetch, display, and save to Supabase)
                    // using (var recognitionManagerService = new RecognitionManagerService())
                    // {
                    //     var recognitionManagers = await RecognitionManagerHandler.ProcessRecognitionManagers(
                    //         recognitionManagerService,
                    //         supabaseService);
                    // }

                    // // 3. Process ALL Data Managers (fetch, display, and save to Supabase)
                    // using (var dataManagerService = new DataManagerService())
                    // {
                    //     var dataManagers = await DataManagerHandler.ProcessDataManagers(
                    //         dataManagerService,
                    //         supabaseService);
                    // }

                    // // 4. Process ALL Validation Codes (fetch, display, and save to Supabase)
                    // using (var validationCodeService = new ValidationCodeService())
                    // {
                    //     var validationCodes = await ValidationCodeHandler.ProcessValidationCodes(
                    //         validationCodeService,
                    //         supabaseService);
                    // }

                    // // 5. Process ALL Contact Roles (fetch, display, and save to Supabase)
                    // using (var contactRoleService = new ContactRoleService())
                    // {
                    //     var contactRoles = await ContactRoleHandler.ProcessContactRoles(
                    //         contactRoleService,
                    //         supabaseService);
                    // }

                    // // 6. Process ALL Address States (fetch, display, and save to Supabase)
                    // using (var addressStateService = new AddressStateService())
                    // {
                    //     var addressStates = await AddressStateHandler.ProcessAddressStates(
                    //         addressStateService,
                    //         supabaseService);
                    // }

                    // // 7. Process Recognition Manager Assignments (via GetDetails)
                    // using (var summaryService = new TrainingComponentSummaryService())
                    // using (var assignmentService = new RecognitionManagerAssignmentService())
                    // {
                    //     var recognitionManagerAssignments = await RecognitionManagerAssignmentHandler.ProcessRecognitionManagerAssignments(
                    //         summaryService,
                    //         assignmentService,
                    //         supabaseService,
                    //         startDate: new DateTime(2016, 1, 15), // 15/01/2016
                    //         endDate: new DateTime(2026, 1, 15),   // 15/01/2026
                    //         maxResults: 0); // 0 = fetch all via pagination
                    // }

                    // 8. Process Data Manager Assignments (via GetDetails)
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

                    // // 9. Process Training Component Summaries (fetch and save to Supabase)
                    // using (var summaryService = new TrainingComponentSummaryService())
                    // {
                    //     var trainingComponentSummaries = await TrainingComponentSummaryHandler.ProcessTrainingComponentSummaries(
                    //         summaryService,
                    //         supabaseService,
                    //         startDate: new DateTime(2016, 1, 15), // 15/01/2016
                    //         endDate: new DateTime(2026, 1, 15),   // 15/01/2026
                    //         maxResults: 0); // 0 = fetch all via pagination
                    // }
                }
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
