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
    /// Handler for Currency Period operations - fetching and saving to database
    /// </summary>
    public static class CurrencyPeriodHandler
    {
        /// <summary>
        /// Fetches currency periods via GetDetails for each training component code
        /// </summary>
        public static async Task<List<CurrencyPeriodRecord>> ProcessCurrencyPeriods(
            TrainingComponentSummaryService summaryService,
            CurrencyPeriodService currencyPeriodService,
            SupabaseService supabaseService,
            DateTime startDate,
            DateTime endDate,
            int maxResults = 0)
        {
            Console.WriteLine("=== Getting and Saving Currency Periods ===");
            Console.WriteLine("(Fetching details per training component code)\n");

            var allCurrencyPeriods = new List<CurrencyPeriodRecord>();
            var saveStopwatch = Stopwatch.StartNew();
            var totalCurrencyPeriodsSaved = 0;

            try
            {
                int totalProcessed = await summaryService.SearchByModifiedDateWithCallback(
                    async (pageResults, pageNumber, totalSoFar) =>
                    {
                        var pageByKey = new Dictionary<string, CurrencyPeriodRecord>(StringComparer.OrdinalIgnoreCase);

                        foreach (var summary in pageResults)
                        {
                            var periods = currencyPeriodService.GetCurrencyPeriods(summary.Code);
                            foreach (var period in periods)
                            {
                                var record = new CurrencyPeriodRecord
                                {
                                    CurrencyPeriodKey = BuildCurrencyPeriodKey(summary.Code, period),
                                    TrainingComponentCode = summary.Code,
                                    Authority = period.Authority,
                                    EndComment = (period as NrtCurrencyPeriod2)?.EndComment,
                                    EndReasonCode = (period as NrtCurrencyPeriod2)?.EndReasonCode,
                                    ActionOnEntity = period.ActionOnEntity.ToString(),
                                    StartDate = period.StartDate,
                                    EndDate = period.EndDate
                                };

                                // De-duplicate within the same batch
                                pageByKey[record.CurrencyPeriodKey] = record;
                            }
                        }

                        var pageCurrencyPeriods = pageByKey.Values.ToList();
                        Console.WriteLine($"  Saving Page {pageNumber} (processed {pageResults.Length} components, {pageCurrencyPeriods.Count} currency periods) to Supabase...");
                        if (pageCurrencyPeriods.Count > 0)
                        {
                            await supabaseService.SaveToSupabase(pageCurrencyPeriods.ToArray(), "currency_periods");
                        }
                        totalCurrencyPeriodsSaved += pageCurrencyPeriods.Count;
                        Console.WriteLine($"  ✓ Page {pageNumber} saved successfully! (Total components processed: {totalSoFar + pageResults.Length}, total currency periods saved: {totalCurrencyPeriodsSaved})");
                        Console.WriteLine();

                        allCurrencyPeriods.AddRange(pageCurrencyPeriods);
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
                Console.WriteLine($"✓ Saved {allCurrencyPeriods.Count} currency periods to Supabase!");
                Console.WriteLine($"Time taken to save: {saveStopwatch.Elapsed}\n");
                Console.ForegroundColor = originalColor;

                return allCurrencyPeriods;
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
                Console.WriteLine($"\nNote: {allCurrencyPeriods.Count} currency periods were saved before the error occurred.\n");
                return allCurrencyPeriods.Count > 0 ? allCurrencyPeriods : null;
            }
        }

        private static string BuildCurrencyPeriodKey(string trainingComponentCode, NrtCurrencyPeriod period)
        {
            var period2 = period as NrtCurrencyPeriod2;
            var raw = string.Join("|", new[]
            {
                trainingComponentCode ?? string.Empty,
                period.Authority ?? string.Empty,
                period.StartDate ?? string.Empty,
                period.EndDate ?? string.Empty,
                period2?.EndComment ?? string.Empty,
                period2?.EndReasonCode ?? string.Empty
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
