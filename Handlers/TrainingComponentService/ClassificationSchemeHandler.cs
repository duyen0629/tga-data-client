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
    /// Handler for Classification Scheme operations - fetching and saving to database
    /// </summary>
    public static class ClassificationSchemeHandler
    {
        /// <summary>
        /// Fetches all classification schemes and saves them to Supabase
        /// </summary>
        public static async Task<List<ClassificationSchemeRecord>> ProcessClassificationSchemes(
            ClassificationSchemeService schemeService,
            SupabaseService supabaseService)
        {
            Console.WriteLine("=== Getting Classification Schemes ===");
            var schemes = schemeService.GetClassificationSchemes();

            Console.WriteLine(" Count of Classification Schemes:" + schemes.Length);
            if (schemes == null || schemes.Length == 0)
            {
                Console.WriteLine("No classification schemes found.\n");
                return null;
            }

            var schemeRecords = new List<ClassificationSchemeRecord>();
            var valueByKey = new Dictionary<string, ClassificationSchemeValueRecord>(StringComparer.OrdinalIgnoreCase);

            foreach (var scheme in schemes)
            {
                var values = scheme.ClassificationValues ?? Array.Empty<ClassificationValue>();

                schemeRecords.Add(new ClassificationSchemeRecord
                {
                    SchemeCode = scheme.SchemeCode,
                    Name = scheme.Name,
                    Description = scheme.Description,
                    AllowMultipleValues = scheme.AllowMultipleValues,
                    IsProtected = scheme.IsProtected,
                    AppliesToComponentTypes = scheme.AppliesToComponentTypes.ToString(),
                    RequiredForComponentTypes = scheme.RequiredForComponentTypes.ToString(),
                    ClassificationValuesCount = values.Length,
                    ExtensionData = scheme.ExtensionData
                });

                foreach (var value in values)
                {
                    var record = new ClassificationSchemeValueRecord
                    {
                        ClassificationValueKey = BuildClassificationValueKey(scheme.SchemeCode, value),
                        SchemeCode = scheme.SchemeCode,
                        Value = value.Value,
                        Name = value.Name,
                        Description = value.Description,
                        DisplayOrder = value.DisplayOrder,
                        ActionOnEntity = value.ActionOnEntity.ToString(),
                        StartDate = value.StartDate,
                        EndDate = value.EndDate,
                        ExtensionData = value.ExtensionData
                    };

                    valueByKey[record.ClassificationValueKey] = record;
                }
            }

            Console.WriteLine("=== Saving Classification Schemes to Supabase ===");
            try
            {
                var saveStopwatch = Stopwatch.StartNew();
                await supabaseService.SaveToSupabase(schemeRecords.ToArray(), "classification_schemes");

                var schemeValues = valueByKey.Values.ToList();
                if (schemeValues.Count > 0)
                {
                    await supabaseService.SaveToSupabase(schemeValues.ToArray(), "classification_scheme_values");
                }
                saveStopwatch.Stop();

                var originalColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\n✓ Successfully saved {schemeRecords.Count} classification schemes to Supabase!");
                Console.WriteLine($"✓ Saved {valueByKey.Count} classification scheme values to Supabase!");
                Console.WriteLine($"Time taken to save: {saveStopwatch.Elapsed}\n");
                Console.ForegroundColor = originalColor;

                return schemeRecords;
            }
            catch (Exception supabaseEx)
            {
                Console.WriteLine($"ERROR: Failed to save to Supabase: {supabaseEx.Message}");
                if (supabaseEx.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {supabaseEx.InnerException.Message}");
                }
                Console.WriteLine("Continuing with rest of the application...\n");
                return schemeRecords.Count > 0 ? schemeRecords : null;
            }
        }

        private static string BuildClassificationValueKey(string schemeCode, ClassificationValue value)
        {
            var raw = string.Join("|", new[]
            {
                schemeCode ?? string.Empty,
                value?.Value ?? string.Empty,
                value?.Name ?? string.Empty,
                value?.Description ?? string.Empty,
                value?.DisplayOrder.ToString() ?? string.Empty,
                value?.ActionOnEntity.ToString() ?? string.Empty,
                value?.StartDate ?? string.Empty,
                value?.EndDate ?? string.Empty
            });

            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
