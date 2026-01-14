using System;
using System.Threading.Tasks;
using TgaGateway2.Handlers;
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
                    var recognitionManagers = await RecognitionManagerHandler.ProcessRecognitionManagers(
                        tgaService,
                        supabaseService);

                    // 3. Get Training Component Details
                    // NOTE: Replace "BSB40520" with an actual training component code
                    string trainingComponentCode = "BSB40520";
                    TrainingComponentHandler.ProcessTrainingComponentDetails(
                        tgaService,
                        trainingComponentCode,
                        recognitionManagers,
                        showReleases: true,
                        showRecognitionManagers: true,
                        showContacts: true,
                        showClassifications: true,
                        showFullStructure: true
                    );
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
