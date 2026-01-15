using System;
using System.Diagnostics;
using System.Threading.Tasks;
using TgaGateway2.Services;
using training.gov.au.services;

namespace TgaGateway2.Handlers.TrainingComponentService
{
    /// <summary>
    /// Handler for DataManager operations - fetching and saving to database
    /// </summary>
    public static class DataManagerHandler
    {
        /// <summary>
        /// Fetches all DataManagers from TGA service, displays them, and saves to Supabase
        /// </summary>
        public static async Task<DataManager[]> ProcessDataManagers(
            DataManagerService dataManagerService,
            SupabaseService supabaseService)
        {
            Console.WriteLine("=== Getting Data Managers ===");
            var dataManagers = dataManagerService.GetDataManagers();

            Console.WriteLine(" Count of Data Managers:" + dataManagers.Length);
            if (dataManagers != null && dataManagers.Length > 0)
            {
                foreach (var dm in dataManagers)
                {
                    Console.WriteLine($"Code: {dm.Code}");
                    Console.WriteLine($"Description: {dm.Description}");
                    Console.WriteLine($"RecognitionManagerCode: {dm.RecognitionManagerCode}");
                    Console.WriteLine($"RegistrationManagerCode: {dm.RegistrationManagerCode}");
                    Console.WriteLine();
                }

                Console.WriteLine("=== Saving Data Managers to Supabase ===");
                try
                {
                    var saveStopwatch = Stopwatch.StartNew();
                    await supabaseService.SaveToSupabase(dataManagers, "data_managers");
                    saveStopwatch.Stop();

                    var originalColor = Console.ForegroundColor;
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"\n✓ Successfully saved {dataManagers.Length} Data Managers to Supabase!");
                    Console.WriteLine($"Time taken to save: {saveStopwatch.Elapsed}\n");
                    Console.ForegroundColor = originalColor;
                }
                catch (Exception supabaseEx)
                {
                    Console.WriteLine($"ERROR: Failed to save to Supabase: {supabaseEx.Message}");
                    if (supabaseEx.InnerException != null)
                    {
                        Console.WriteLine($"Inner Exception: {supabaseEx.InnerException.Message}");
                    }
                    Console.WriteLine("Continuing with rest of the application...\n");
                }

                return dataManagers;
            }

            Console.WriteLine("No data managers found.\n");
            return null;
        }
    }
}
