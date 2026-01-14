using System;
using System.Threading.Tasks;
using TgaGateway2.Handlers.TrainingComponentService;
using TgaGateway2.Services;

namespace TgaGateway2
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
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

                    // 2. Process ALL Recognition Managers (fetch, display, and save to Supabase)
                    using (var recognitionManagerService = new RecognitionManagerService())
                    {
                        var recognitionManagers = await RecognitionManagerHandler.ProcessRecognitionManagers(
                            recognitionManagerService,
                            supabaseService);
                    }

                    // 3. Process Training Component Summaries (fetch and save to Supabase)
                    using (var summaryService = new TrainingComponentSummaryService())
                    {
                        var trainingComponentSummaries = await TrainingComponentSummaryHandler.ProcessTrainingComponentSummaries(
                            summaryService,
                            supabaseService,
                            startDate: DateTime.Now.AddYears(-10), // Search last 10 years
                            endDate: DateTime.Now,
                            maxResults: 100); // 0 = fetch all via pagination
                    }
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

            Console.WriteLine();
            Console.Write("Press Enter to exit...");
            Console.ReadLine();
        }
    }
}
