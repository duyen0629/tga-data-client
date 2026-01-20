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

namespace TgaGateway2.Handlers.Classification
{
    /// <summary>
    /// Handler for ClassificationService scheme searches - fetching and saving to database
    /// </summary>
    public static class ClassificationSchemeSearchHandler
    {
        /// <summary>
        /// Searches NRT and RTO classifications by scheme codes and saves to Supabase
        /// </summary>
        public static async Task ProcessClassificationSchemes(
            ClassificationSchemeService schemeService,
            TgaClassificationService classificationService,
            SupabaseService supabaseService)
        {
            Console.WriteLine("=== Getting Classification Schemes (Classification Service) ===");

            var schemes = schemeService.GetClassificationSchemes();
            if (schemes == null || schemes.Length == 0)
            {
                Console.WriteLine("No classification schemes found.\n");
                return;
            }

            var nrtSchemes = new Dictionary<string, NrtClassificationSchemeRecord>(StringComparer.OrdinalIgnoreCase);
            var nrtValuesByKey = new Dictionary<string, NrtClassificationSchemeValueRecord>(StringComparer.OrdinalIgnoreCase);
            var rtoSchemes = new Dictionary<string, RtoClassificationSchemeRecord>(StringComparer.OrdinalIgnoreCase);
            var rtoValuesByKey = new Dictionary<string, RtoClassificationSchemeValueRecord>(StringComparer.OrdinalIgnoreCase);

            foreach (var scheme in schemes)
            {
                var schemeCode = scheme.SchemeCode;
                if (string.IsNullOrWhiteSpace(schemeCode))
                {
                    continue;
                }

                // NRT classifications
                try
                {
                    var nrtResult = classificationService.SearchNrtClassificationsByScheme(schemeCode);
                    if (nrtResult != null)
                    {
                        var nrtValues = nrtResult.ClassificationValues ?? Array.Empty<ClassificationValue>();
                        nrtSchemes[schemeCode] = new NrtClassificationSchemeRecord
                        {
                            SchemeCode = nrtResult.SchemeCode,
                            Name = nrtResult.Name,
                            Description = nrtResult.Description,
                            AllowMultipleValues = nrtResult.AllowMultipleValues,
                            IsProtected = nrtResult.IsProtected,
                            AppliesToComponentTypes = nrtResult.AppliesToComponentTypes.ToString(),
                            RequiredForComponentTypes = nrtResult.RequiredForComponentTypes.ToString(),
                            ClassificationValuesCount = nrtValues.Length,
                            ExtensionData = nrtResult.ExtensionData
                        };

                        foreach (var value in nrtValues)
                        {
                            var record = new NrtClassificationSchemeValueRecord
                            {
                                ClassificationValueKey = BuildClassificationValueKey("NRT", schemeCode, value),
                                SchemeCode = schemeCode,
                                Value = value.Value,
                                Name = value.Name,
                                Description = value.Description,
                                DisplayOrder = value.DisplayOrder,
                                ActionOnEntity = value.ActionOnEntity.ToString(),
                                StartDate = value.StartDate,
                                EndDate = value.EndDate,
                                ExtensionData = value.ExtensionData
                            };

                            nrtValuesByKey[record.ClassificationValueKey] = record;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ✗ NRT scheme {schemeCode} failed: {ex.Message}");
                }

                // RTO classifications
                try
                {
                    var rtoResult = classificationService.SearchRtoClassificationsByScheme(schemeCode);
                    if (rtoResult != null)
                    {
                        var rtoValues = rtoResult.ClassificationValues ?? Array.Empty<ClassificationValue>();
                        rtoSchemes[schemeCode] = new RtoClassificationSchemeRecord
                        {
                            SchemeCode = rtoResult.SchemeCode,
                            Name = rtoResult.Name,
                            Description = rtoResult.Description,
                            AllowMultipleValues = rtoResult.AllowMultipleValues,
                            IsProtected = rtoResult.IsProtected,
                            IsRequired = rtoResult.IsRequired,
                            ClassificationValuesCount = rtoValues.Length,
                            ExtensionData = rtoResult.ExtensionData
                        };

                        foreach (var value in rtoValues)
                        {
                            var record = new RtoClassificationSchemeValueRecord
                            {
                                ClassificationValueKey = BuildClassificationValueKey("RTO", schemeCode, value),
                                SchemeCode = schemeCode,
                                Value = value.Value,
                                Name = value.Name,
                                Description = value.Description,
                                DisplayOrder = value.DisplayOrder,
                                ActionOnEntity = value.ActionOnEntity.ToString(),
                                StartDate = value.StartDate,
                                EndDate = value.EndDate,
                                ExtensionData = value.ExtensionData
                            };

                            rtoValuesByKey[record.ClassificationValueKey] = record;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ✗ RTO scheme {schemeCode} failed: {ex.Message}");
                }
            }

            Console.WriteLine("=== Saving Classification Schemes (Classification Service) to Supabase ===");
            try
            {
                var saveStopwatch = Stopwatch.StartNew();

                if (nrtSchemes.Count > 0)
                {
                    await supabaseService.SaveToSupabase(nrtSchemes.Values.ToArray(), "nrt_classification_schemes");
                }
                if (nrtValuesByKey.Count > 0)
                {
                    await supabaseService.SaveToSupabase(nrtValuesByKey.Values.ToArray(), "nrt_classification_scheme_values");
                }

                if (rtoSchemes.Count > 0)
                {
                    await supabaseService.SaveToSupabase(rtoSchemes.Values.ToArray(), "rto_classification_schemes");
                }
                if (rtoValuesByKey.Count > 0)
                {
                    await supabaseService.SaveToSupabase(rtoValuesByKey.Values.ToArray(), "rto_classification_scheme_values");
                }

                saveStopwatch.Stop();

                var originalColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\n✓ Saved {nrtSchemes.Count} NRT schemes and {nrtValuesByKey.Count} NRT values.");
                Console.WriteLine($"✓ Saved {rtoSchemes.Count} RTO schemes and {rtoValuesByKey.Count} RTO values.");
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
        }

        private static string BuildClassificationValueKey(string scope, string schemeCode, ClassificationValue value)
        {
            var raw = string.Join("|", new[]
            {
                scope ?? string.Empty,
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
