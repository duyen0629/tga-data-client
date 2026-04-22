using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Xml.Linq;
using TgaGateway2.Handlers.TrainingComponentDocuments.Helper;
using TgaGateway2.Handlers.TrainingComponentDocuments.Parser;
using TgaGateway2.Handlers.TrainingComponentDocuments.Type;
using TgaGateway2.Models;
using TgaGateway2.Services;
using UglyToad.PdfPig;

namespace TgaGateway2.Handlers.TrainingComponentDocuments
{
    public static class TrainingComponentDocumentHandler
    {
        public static async Task ProcessTrainingComponentDocumentForCode(
            SupabaseService supabaseService,
            string trainingComponentCode)
        {
            if (string.IsNullOrWhiteSpace(trainingComponentCode))
            {
                throw new ArgumentException("Training component code is required.", nameof(trainingComponentCode));
            }

            Console.WriteLine($"  == Processing Training Component Document: {trainingComponentCode} ==  ");
            try
            {
                var queryService = new SupabaseQueryService();
                var releaseFiles = await queryService.GetReleaseFilesByCode(trainingComponentCode);

                if (releaseFiles == null || releaseFiles.Count == 0)
                {
                    Console.WriteLine("No release files found.");
                    return;
                }

                var summaryFields = await queryService.GetSummaryFieldsForDocumentByCode(trainingComponentCode);
                var componentType = summaryFields.ComponentType;
                var usageRecommendation = summaryFields.UsageRecommendation;
                if (string.Equals(componentType, "Qualification", StringComparison.OrdinalIgnoreCase))
                {
                    await ProcessReleaseFilesForTrainingComponentDocumentAsQualification(supabaseService, trainingComponentCode, releaseFiles, componentType, usageRecommendation);
                }
                else
                {
                    await ProcessReleaseFilesForTrainingComponentDocumentAsUnit(supabaseService, trainingComponentCode, releaseFiles, componentType, usageRecommendation);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    Failed to process {trainingComponentCode}. {ProcessErrorHelper.BuildProcessError(ex)}");
            }
        }

        public static async Task ProcessQualificationDocumentForCode(SupabaseService supabaseService, string trainingComponentCode)
        {
            if (string.IsNullOrWhiteSpace(trainingComponentCode))
            {
                throw new ArgumentException("Training component code is required.", nameof(trainingComponentCode));
            }

            Console.WriteLine($"  == Processing Qualification Document: {trainingComponentCode} ==  ");
            try
            {
                var queryService = new SupabaseQueryService();
                var releaseFiles = await queryService.GetReleaseFilesByCode(trainingComponentCode);
                if (releaseFiles == null || releaseFiles.Count == 0)
                {
                    Console.WriteLine("No release files found.");
                    return;
                }

                var summaryFields = await queryService.GetSummaryFieldsForDocumentByCode(trainingComponentCode);
                var componentType = summaryFields.ComponentType;
                var usageRecommendation = summaryFields.UsageRecommendation;
                if (!string.Equals(componentType, "Qualification", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"  Code {trainingComponentCode} is not a Qualification (component_type={componentType ?? "null"}). Skipping.");
                    return;
                }

                await ProcessReleaseFilesForTrainingComponentDocumentAsQualification(supabaseService, trainingComponentCode, releaseFiles, componentType, usageRecommendation);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    Failed to process Qualification {trainingComponentCode}. {ProcessErrorHelper.BuildProcessError(ex)}");
            }
        }

        public static async Task ProcessTrainingComponentDocumentsForAll(SupabaseService supabaseService, int pageOffset, int batchSize)
        {
            Console.WriteLine("===  Getting and Saving Training Component Documents (Unit and Qualification) ===");

            var saveStopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                var queryService = new SupabaseQueryService();
                Console.WriteLine();

                var offset = Math.Max(0, pageOffset) * batchSize;
                var totalReleaseFilesProcessed = 0;
                var totalTrainingDocumentsSaved = 0;
                var pageNumber = Math.Max(0, pageOffset);

                while (true)
                {
                    pageNumber++;
                    Console.WriteLine($" Attempting search release files - Page {pageNumber}, PageSize {batchSize}...");
                    var releaseFiles = await queryService.GetReleaseFilesPage(batchSize, offset);

                    if (releaseFiles.Count == 0)
                    {
                        break;
                    }

                    var grouped = releaseFiles
                        .Where(r => !string.IsNullOrWhiteSpace(r.training_component_code))
                        .GroupBy(r => r.training_component_code)
                        .OrderBy(g => g.Key)
                        .ToList();

                    Console.WriteLine($"  Page {pageNumber}: Found {grouped.Count} training code");

                    var pageSaved = 0;
                    foreach (var group in grouped)
                    {
                        string componentType = null;
                        string usageRecommendation = null;
                        try
                        {
                            var summaryFields = await queryService.GetSummaryFieldsForDocumentByCode(group.Key);
                            componentType = summaryFields.ComponentType;
                            usageRecommendation = summaryFields.UsageRecommendation;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"   Could not get summary fields for {group.Key}, skipping: {ex.Message}");
                            continue;
                        }

                        if (string.Equals(componentType, "Unit", StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                var saved = await ProcessReleaseFilesForTrainingComponentDocumentAsUnit(supabaseService, group.Key, group.ToList(), componentType, usageRecommendation);
                                pageSaved += saved;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"    Failed to process Unit {group.Key}. {ProcessErrorHelper.BuildProcessError(ex)}");
                            }
                            continue;
                        }

                        if (string.Equals(componentType, "Qualification", StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                var saved = await ProcessReleaseFilesForTrainingComponentDocumentAsQualification(supabaseService, group.Key, group.ToList(), componentType, usageRecommendation);
                                pageSaved += saved;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"    Failed to process Qualification {group.Key}. {ProcessErrorHelper.BuildProcessError(ex)}");
                            }
                            continue;
                        }

                        // Other component types skipped
                    }

                    totalReleaseFilesProcessed += releaseFiles.Count;
                    totalTrainingDocumentsSaved += pageSaved;
                    Console.WriteLine($"  ✓ Page {pageNumber} saved successfully! (Total release_files processed: {totalReleaseFilesProcessed}, training code: {grouped.Count}, total training document saved: {totalTrainingDocumentsSaved})");
                    Console.WriteLine();

                    if (releaseFiles.Count < batchSize)
                    {
                        break;
                    }

                    offset += batchSize;
                }

                saveStopwatch.Stop();

                var originalColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\n✓ Successfully processed {totalTrainingDocumentsSaved} components.");
                Console.WriteLine($"Time taken to save: {saveStopwatch.Elapsed}\n");
                Console.ForegroundColor = originalColor;
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
                Console.WriteLine("\nNote: Some training component documents may have been saved before the error occurred.\n");
            }
        }

        private static async Task<int> ProcessReleaseFilesForTrainingComponentDocumentAsUnit(
            SupabaseService supabaseService,
            string trainingComponentCode,
            List<ReleaseFileRow> releaseFiles,
            string componentType,
            string usageRecommendation)
        {
            var candidates = ReleaseFileHelper.SelectReleaseFilesByRelease(releaseFiles);
            if (candidates.Count == 0)
            {
                Console.WriteLine($"No matching XML file found for {trainingComponentCode}.");
                return 0;
            }

            var savedCount = 0;
            foreach (var candidate in candidates)
            {
                Console.WriteLine($"   Using release {candidate.ReleaseNumber}.");

                try
                {
                    var completeResult = await ReleaseFileHelper.LoadLinesXmlOnly(candidate.Complete);
                    var record = BuildRecordFromXmlBytesForUnit(
                        trainingComponentCode,
                        candidate.ReleaseNumber,
                        componentType,
                        usageRecommendation,
                        completeResult.SelectedRelativePath,
                        completeResult.FormatUsed,
                        completeResult.Bytes);

                    await supabaseService.SaveToSupabase(new[] { record }, "training_component_documents");
                    Console.WriteLine("   ✓ training_component_documents saved.");
                    savedCount++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"     Failed to process {trainingComponentCode} release {candidate.ReleaseNumber}. Saving error record...");
                    var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                    var sourceFiles = new
                    {
                        complete = new
                        {
                            relative_path = candidate?.Complete?.XmlPath,
                            format = "xml"
                        }
                    };
                    var sourceFilesJson = CommonParser.SanitizeJson(serializer.Serialize(sourceFiles));
                    var errorRecord = new TrainingComponentDocumentRecord
                    {
                        TrainingComponentCode = trainingComponentCode,
                        ReleaseNumber = candidate.ReleaseNumber,
                        ComponentType = componentType,
                        UsageRecommendation = usageRecommendation,
                        Title = trainingComponentCode,
                        SourceFiles = new JsonRaw(sourceFilesJson),
                        ContentJson = null,
                        RawXml = null,
                        ParsedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture),
                        ProcessError = ProcessErrorHelper.BuildProcessError(ex)
                    };

                    try
                    {
                        await supabaseService.SaveToSupabase(new[] { errorRecord }, "training_component_documents");
                        Console.WriteLine("   ✓ training_component_documents error saved.");
                    }
                    catch (Exception saveEx)
                    {
                        Console.WriteLine($"     Failed to save error record for {trainingComponentCode} release {candidate.ReleaseNumber}: {ProcessErrorHelper.BuildProcessError(saveEx)}");
                    }
                }
            }

            return savedCount;
        }

        private static async Task<int> ProcessReleaseFilesForTrainingComponentDocumentAsQualification(
            SupabaseService supabaseService,
            string trainingComponentCode,
            List<ReleaseFileRow> releaseFiles,
            string componentType,
            string usageRecommendation)
        {
            var candidates = ReleaseFileHelper.SelectReleaseFilesByRelease(releaseFiles);
            if (candidates.Count == 0)
            {
                Console.WriteLine($"No matching XML file found for {trainingComponentCode}.");
                return 0;
            }

            var savedCount = 0;
            foreach (var candidate in candidates)
            {
                Console.WriteLine($"   Using release {candidate.ReleaseNumber}.");

                try
                {
                    var completeResult = await ReleaseFileHelper.LoadLinesXmlOnly(candidate.Complete);
                    var record = BuildRecordFromXmlBytesForQualification(
                        trainingComponentCode,
                        candidate.ReleaseNumber,
                        componentType,
                        usageRecommendation,
                        completeResult.SelectedRelativePath,
                        completeResult.FormatUsed,
                        completeResult.Bytes);

                    await supabaseService.SaveToSupabase(new[] { record }, "training_component_documents");
                    Console.WriteLine("   ✓ training_component_documents (Qualification) saved.");
                    savedCount++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"     Failed to process Qualification {trainingComponentCode} release {candidate.ReleaseNumber}. Saving error record...");
                    var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                    var sourceFiles = new
                    {
                        complete = new
                        {
                            relative_path = candidate?.Complete?.XmlPath,
                            format = "xml"
                        }
                    };
                    var sourceFilesJson = CommonParser.SanitizeJson(serializer.Serialize(sourceFiles));
                    var errorRecord = new TrainingComponentDocumentRecord
                    {
                        TrainingComponentCode = trainingComponentCode,
                        ReleaseNumber = candidate.ReleaseNumber,
                        ComponentType = componentType,
                        UsageRecommendation = usageRecommendation,
                        Title = trainingComponentCode,
                        SourceFiles = new JsonRaw(sourceFilesJson),
                        ContentJson = null,
                        RawXml = null,
                        ParsedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture),
                        ProcessError = ProcessErrorHelper.BuildProcessError(ex)
                    };

                    try
                    {
                        await supabaseService.SaveToSupabase(new[] { errorRecord }, "training_component_documents");
                        Console.WriteLine("   ✓ training_component_documents error saved.");
                    }
                    catch (Exception saveEx)
                    {
                        Console.WriteLine($"     Failed to save error record for {trainingComponentCode} release {candidate.ReleaseNumber}: {ProcessErrorHelper.BuildProcessError(saveEx)}");
                    }
                }
            }

            return savedCount;
        }

        internal static TrainingComponentDocumentRecord BuildRecordFromXmlBytesForQualification(
            string trainingComponentCode,
            string releaseNumber,
            string componentType,
            string usageRecommendation,
            string relativePath,
            string formatUsed,
            byte[] xmlBytes)
        {
            var lines = ReleaseFileHelper.ExtractLinesFromXml(xmlBytes);
            var title = CommonParser.ExtractTitle(trainingComponentCode, lines) ?? trainingComponentCode;

            var (sections, packagingRules) = QualificationParser.ParserSectionFromXmlForQualification(xmlBytes);

            var sourceFiles = new
            {
                complete = new
                {
                    relative_path = relativePath,
                    format = formatUsed ?? "xml"
                }
            };

            // packing_rules_extracted is created and extracted from the Packaging rules section in sections
            var contentObj = new Dictionary<string, object>
            {
                { "packing_rules_extracted", packagingRules },
                { "sections", sections },
                { "source", sourceFiles }
            };

            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            var sourceFilesJson = CommonParser.SanitizeJson(serializer.Serialize(sourceFiles));
            var contentJsonRaw = CommonParser.SanitizeJson(serializer.Serialize(contentObj));
            var rawXml = xmlBytes != null ? Encoding.UTF8.GetString(xmlBytes) : null;

            return new TrainingComponentDocumentRecord
            {
                TrainingComponentCode = trainingComponentCode,
                ReleaseNumber = releaseNumber,
                ComponentType = componentType,
                UsageRecommendation = usageRecommendation,
                Title = title,
                SourceFiles = new JsonRaw(sourceFilesJson),
                ContentJson = new JsonRaw(contentJsonRaw),
                RawXml = rawXml,
                ParsedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture)
            };
        }

        internal static TrainingComponentDocumentRecord BuildRecordFromXmlBytesForUnit(
            string trainingComponentCode,
            string releaseNumber,
            string componentType,
            string usageRecommendation,
            string relativePath,
            string formatUsed,
            byte[] xmlBytes)
        {
            var lines = ReleaseFileHelper.ExtractLinesFromXml(xmlBytes);
            var title = CommonParser.ExtractTitle(trainingComponentCode, lines) ?? trainingComponentCode;

            var completeSections = UnitParser.ParserSectionFromXmlForUnit(xmlBytes);
            var mergedSections = completeSections;

            var sourceFiles = new
            {
                complete = new
                {
                    relative_path = relativePath,
                    format = formatUsed ?? "xml"
                }
            };

            var contentJson = new
            {
                sections = mergedSections,
                source = sourceFiles
            };

            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            var sourceFilesJson = CommonParser.SanitizeJson(serializer.Serialize(sourceFiles));
            var contentJsonRaw = CommonParser.SanitizeJson(serializer.Serialize(contentJson));
            var rawXml = xmlBytes != null ? Encoding.UTF8.GetString(xmlBytes) : null;

            return new TrainingComponentDocumentRecord
            {
                TrainingComponentCode = trainingComponentCode,
                ReleaseNumber = releaseNumber,
                ComponentType = componentType,
                UsageRecommendation = usageRecommendation,
                Title = title,
                SourceFiles = new JsonRaw(sourceFilesJson),
                ContentJson = new JsonRaw(contentJsonRaw),
                RawXml = rawXml,
                ParsedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture)
            };
        }
    }
}
