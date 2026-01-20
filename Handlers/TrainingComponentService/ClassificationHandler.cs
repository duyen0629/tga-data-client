using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using TgaGateway2.Models;
using TgaGateway2.Services;
using training.gov.au.services;

namespace TgaGateway2.Handlers.TrainingComponentService
{
    /// <summary>
    /// Handler for Classification operations - fetching and saving to database
    /// </summary>
    public static class ClassificationHandler
    {
        /// <summary>
        /// Fetches classifications via GetDetails for each training component code
        /// </summary>
        public static async Task<List<ClassificationRecord>> ProcessClassifications(
            TrainingComponentSummaryService summaryService,
            ClassificationService classificationService,
            SupabaseService supabaseService,
            DateTime startDate,
            DateTime endDate,
            int maxResults = 0)
        {
            Console.WriteLine("=== Getting and Saving Classifications ===");
            Console.WriteLine("(Fetching details per training component code)\n");

            var allClassifications = new List<ClassificationRecord>();
            var saveStopwatch = Stopwatch.StartNew();
            var totalClassificationsSaved = 0;

            try
            {
                int totalProcessed = await summaryService.SearchByModifiedDateWithCallback(
                    async (pageResults, pageNumber, totalSoFar) =>
                    {
                        var pageByKey = new Dictionary<string, ClassificationRecord>(StringComparer.OrdinalIgnoreCase);

                        foreach (var summary in pageResults)
                        {
                            var classifications = classificationService.GetClassifications(summary.Code);
                            foreach (var classification in classifications)
                            {
                                var record = new ClassificationRecord
                                {
                                    ClassificationKey = BuildClassificationKey(summary.Code, classification),
                                    TrainingComponentCode = summary.Code,
                                    PurposeCode = classification.PurposeCode,
                                    SchemeCode = classification.SchemeCode,
                                    ValueCode = classification.ValueCode,
                                    ActionOnEntity = classification.ActionOnEntity.ToString(),
                                    StartDate = classification.StartDate,
                                    EndDate = classification.EndDate
                                };

                                // De-duplicate within the same batch
                                pageByKey[record.ClassificationKey] = record;
                            }
                        }

                        var pageClassifications = pageByKey.Values.ToList();
                        Console.WriteLine($"  Saving Page {pageNumber} (processed {pageResults.Length} components, {pageClassifications.Count} classifications) to Supabase...");
                        if (pageClassifications.Count > 0)
                        {
                            await supabaseService.SaveToSupabase(pageClassifications.ToArray(), "classifications");
                        }
                        totalClassificationsSaved += pageClassifications.Count;
                        Console.WriteLine($"  ✓ Page {pageNumber} saved successfully! (Total components processed: {totalSoFar + pageResults.Length}, total classifications saved: {totalClassificationsSaved})");
                        Console.WriteLine();

                        allClassifications.AddRange(pageClassifications);
                    },
                    startDate,
                    endDate,
                    maxResults,
                    pageSize: 200);

                if (totalProcessed == 0)
                {
                    Console.WriteLine("No training component summaries found.\n");
                    return null;
                }

                saveStopwatch.Stop();

                var originalColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\n✓ Successfully processed {totalProcessed} components.");
                Console.WriteLine($"✓ Saved {allClassifications.Count} classifications to Supabase!");
                Console.WriteLine($"Time taken to save: {saveStopwatch.Elapsed}\n");
                Console.ForegroundColor = originalColor;

                return allClassifications;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n✗ ERROR: Failed during processing!");
                Console.WriteLine($"Exception Type: {ex.GetType().Name}");
                Console.WriteLine($"Exception Message: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                Console.WriteLine($"\nNote: {allClassifications.Count} classifications were saved before the error occurred.\n");
                return allClassifications.Count > 0 ? allClassifications : null;
            }
        }

        private static string BuildClassificationKey(string trainingComponentCode, training.gov.au.services.Classification classification)
        {
            var raw = string.Join("|", new[]
            {
                trainingComponentCode ?? string.Empty,
                classification.PurposeCode ?? string.Empty,
                classification.SchemeCode ?? string.Empty,
                classification.ValueCode ?? string.Empty,
                classification.StartDate ?? string.Empty,
                classification.EndDate ?? string.Empty
            });

            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
                var builder = new StringBuilder(hash.Length * 2);
                foreach (var b in hash)
                {
                    builder.Append(b.ToString("x2"));
                }
                return builder.ToString();
            }
        }
    }
}
