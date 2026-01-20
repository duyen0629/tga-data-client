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
    /// Handler for Lookup operations - fetching and saving to database
    /// </summary>
    public static class LookupHandler
    {
        /// <summary>
        /// Fetches lookup values for all lookup names and saves them to Supabase
        /// </summary>
        public static async Task<List<LookupRecord>> ProcessLookups(
            LookupService lookupService,
            SupabaseService supabaseService,
            LookupName[] lookupNames = null)
        {
            Console.WriteLine("=== Getting Lookups ===");

            var namesToFetch = lookupNames ?? (LookupName[])Enum.GetValues(typeof(LookupName));
            var allLookups = new Dictionary<string, LookupRecord>(StringComparer.OrdinalIgnoreCase);
            var saveStopwatch = Stopwatch.StartNew();

            foreach (var lookupName in namesToFetch)
            {
                Lookup[] lookups;
                try
                {
                    lookups = lookupService.GetLookup(lookupName) ?? Array.Empty<Lookup>();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ✗ Lookup {lookupName} failed: {ex.Message}");
                    continue;
                }

                Console.WriteLine($"  Lookup {lookupName}: {lookups.Length} items");

                foreach (var item in lookups)
                {
                    var record = new LookupRecord
                    {
                        LookupKey = BuildLookupKey(lookupName, item),
                        LookupName = lookupName.ToString(),
                        Code = item.Code,
                        Description = item.Description,
                        ExtensionData = item.ExtensionData
                    };

                    allLookups[record.LookupKey] = record;
                }
            }

            if (allLookups.Count == 0)
            {
                Console.WriteLine("No lookups found.\n");
                return null;
            }

            Console.WriteLine("=== Saving Lookups to Supabase ===");
            try
            {
                await supabaseService.SaveToSupabase(allLookups.Values.ToArray(), "lookups");
                saveStopwatch.Stop();

                var originalColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\n✓ Successfully saved {allLookups.Count} lookups to Supabase!");
                Console.WriteLine($"Time taken to save: {saveStopwatch.Elapsed}\n");
                Console.ForegroundColor = originalColor;

                return allLookups.Values.ToList();
            }
            catch (Exception supabaseEx)
            {
                Console.WriteLine($"ERROR: Failed to save to Supabase: {supabaseEx.Message}");
                if (supabaseEx.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {supabaseEx.InnerException.Message}");
                }
                Console.WriteLine("Continuing with rest of the application...\n");
                return allLookups.Count > 0 ? allLookups.Values.ToList() : null;
            }
        }

        private static string BuildLookupKey(LookupName lookupName, Lookup item)
        {
            var raw = string.Join("|", new[]
            {
                lookupName.ToString(),
                item?.Code ?? string.Empty,
                item?.Description ?? string.Empty
            });

            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
