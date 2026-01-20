using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using TgaGateway2.Models;
using TgaGateway2.Services;
using training.gov.au.services;

namespace TgaGateway2.Handlers.TrainingComponentService
{
    /// <summary>
    /// Handler for Classification Purpose operations - fetching and saving to database
    /// </summary>
    public static class ClassificationPurposeHandler
    {
        /// <summary>
        /// Fetches all classification purposes and saves them to Supabase
        /// </summary>
        public static async Task<ClassificationPurposeRecord[]> ProcessClassificationPurposes(
            ClassificationPurposeService purposeService,
            SupabaseService supabaseService)
        {
            Console.WriteLine("=== Getting Classification Purposes ===");
            var purposes = purposeService.GetClassificationPurposes();

            Console.WriteLine(" Count of Classification Purposes:" + purposes.Length);
            if (purposes == null || purposes.Length == 0)
            {
                Console.WriteLine("No classification purposes found.\n");
                return null;
            }

            var purposeRecords = new List<ClassificationPurposeRecord>();
            foreach (var purpose in purposes)
            {
                purposeRecords.Add(new ClassificationPurposeRecord
                {
                    PurposeCode = purpose.PurposeCode,
                    Description = purpose.Description,
                    ExtensionData = purpose.ExtensionData
                });
            }

            Console.WriteLine("=== Saving Classification Purposes to Supabase ===");
            try
            {
                var saveStopwatch = Stopwatch.StartNew();
                await supabaseService.SaveToSupabase(purposeRecords.ToArray(), "classification_purposes");
                saveStopwatch.Stop();

                var originalColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\n✓ Successfully saved {purposeRecords.Count} classification purposes to Supabase!");
                Console.WriteLine($"Time taken to save: {saveStopwatch.Elapsed}\n");
                Console.ForegroundColor = originalColor;

                return purposeRecords.ToArray();
            }
            catch (Exception supabaseEx)
            {
                Console.WriteLine($"ERROR: Failed to save to Supabase: {supabaseEx.Message}");
                if (supabaseEx.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {supabaseEx.InnerException.Message}");
                }
                Console.WriteLine("Continuing with rest of the application...\n");
                return purposeRecords.Count > 0 ? purposeRecords.ToArray() : null;
            }
        }
    }
}
