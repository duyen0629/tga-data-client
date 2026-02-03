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
using TgaGateway2.Models;
using TgaGateway2.Services;
using UglyToad.PdfPig;

namespace TgaGateway2.Handlers.TrainingComponentDocuments
{
    /// <summary>
    /// Handler to download, parse, and save merged training component documents.
    /// </summary>
    public static class TrainingComponentDocumentHandler
    {
        private const string TrainingComponentFilesBaseUrl = "https://training.gov.au/TrainingComponentFiles/";
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

                await ProcessTrainingComponentDocumentForReleaseFiles(supabaseService, trainingComponentCode, releaseFiles);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠ Failed to process {trainingComponentCode}. {BuildProcessError(ex)}");
            }
        }

        public static async Task ProcessTrainingComponentDocumentsForAll(SupabaseService supabaseService, int pageOffset, int batchSize)
        {
            Console.WriteLine("===  Getting and Saving Training Component Documents ===");

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
                        try
                        {
                            await ProcessTrainingComponentDocumentForCode(supabaseService, group.Key);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"  ⚠ Failed to process {group.Key}. {BuildProcessError(ex)}");
                        }
                        pageSaved++;
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

        private static async Task<int> ProcessTrainingComponentDocumentForReleaseFiles(
            SupabaseService supabaseService,
            string trainingComponentCode,
            List<ReleaseFileRow> releaseFiles)
        {
            var candidates = SelectReleaseFilesByRelease(releaseFiles);
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
                    var completeResult = await LoadLinesXmlOnly(candidate.Complete);
                    var record = BuildRecordFromXmlBytes(
                        trainingComponentCode,
                        candidate.ReleaseNumber,
                        completeResult.SelectedRelativePath,
                        completeResult.FormatUsed,
                        completeResult.Bytes);

                    await supabaseService.SaveToSupabase(new[] { record }, "training_component_documents");
                    Console.WriteLine("   ✓ training_component_documents saved.");
                    savedCount++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ⚠ Failed to process {trainingComponentCode} release {candidate.ReleaseNumber}. Saving error record...");
                    var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                    var sourceFiles = new
                    {
                        complete = new
                        {
                            relative_path = candidate?.Complete?.XmlPath,
                            format = "xml"
                        }
                    };
                    var sourceFilesJson = SanitizeJson(serializer.Serialize(sourceFiles));
                    var errorRecord = new TrainingComponentDocumentRecord
                    {
                        TrainingComponentCode = trainingComponentCode,
                        ReleaseNumber = candidate.ReleaseNumber,
                        Title = trainingComponentCode,
                        SourceFiles = new JsonRaw(sourceFilesJson),
                        ContentJson = null,
                        RawXml = null,
                        ParsedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture),
                        ProcessError = BuildProcessError(ex)
                    };

                    try
                    {
                        await supabaseService.SaveToSupabase(new[] { errorRecord }, "training_component_documents");
                        Console.WriteLine("   ✓ training_component_documents error saved.");
                    }
                    catch (Exception saveEx)
                    {
                        Console.WriteLine($"   ⚠ Failed to save error record for {trainingComponentCode} release {candidate.ReleaseNumber}: {BuildProcessError(saveEx)}");
                    }
                }
            }

            return savedCount;
        }

        internal static List<TrainingComponentDocumentRecord> BuildRecordsForReleaseFilesForTest(
            string trainingComponentCode,
            List<ReleaseFileRow> releaseFiles,
            Func<string, byte[]> xmlBytesProvider)
        {
            if (xmlBytesProvider == null)
            {
                throw new ArgumentNullException(nameof(xmlBytesProvider));
            }

            var candidates = SelectReleaseFilesByRelease(releaseFiles);
            var records = new List<TrainingComponentDocumentRecord>();

            foreach (var candidate in candidates)
            {
                var xmlPath = candidate?.Complete?.XmlPath;
                if (string.IsNullOrWhiteSpace(xmlPath))
                {
                    continue;
                }

                var xmlBytes = xmlBytesProvider(xmlPath);
                if (xmlBytes == null || xmlBytes.Length == 0)
                {
                    throw new Exception($"Missing XML bytes for {xmlPath}");
                }

                var record = BuildRecordFromXmlBytes(
                    trainingComponentCode,
                    candidate.ReleaseNumber,
                    xmlPath,
                    "xml",
                    xmlBytes);

                records.Add(record);
            }

            return records;
        }

        private static TrainingComponentDocumentRecord BuildRecordFromXmlBytes(
            string trainingComponentCode,
            string releaseNumber,
            string relativePath,
            string formatUsed,
            byte[] xmlBytes)
        {
            var lines = ExtractLinesFromXml(xmlBytes);
            var title = ExtractTitle(trainingComponentCode, lines) ?? trainingComponentCode;

            var completeSections = ParseSectionsFromXml(xmlBytes);
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
            var sourceFilesJson = SanitizeJson(serializer.Serialize(sourceFiles));
            var contentJsonRaw = SanitizeJson(serializer.Serialize(contentJson));
            var rawXml = xmlBytes != null ? Encoding.UTF8.GetString(xmlBytes) : null;

            return new TrainingComponentDocumentRecord
            {
                TrainingComponentCode = trainingComponentCode,
                ReleaseNumber = releaseNumber,
                Title = title,
                SourceFiles = new JsonRaw(sourceFilesJson),
                ContentJson = new JsonRaw(contentJsonRaw),
                RawXml = rawXml,
                ParsedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture)
            };
        }

        private static bool IsStatementTimeout(Exception ex)
        {
            var message = ex?.Message ?? string.Empty;
            return message.IndexOf("57014", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("statement timeout", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string BuildProcessError(Exception ex)
        {
            if (ex == null)
            {
                return "Unknown error.";
            }

            var innerMessage = ex.InnerException != null ? ex.InnerException.Message : null;
            var typeName = ex.GetType().Name;
            var summary = $"[{typeName}] {ex.Message}";

            if (!string.IsNullOrWhiteSpace(innerMessage))
            {
                summary += $" | Inner: {innerMessage}";
            }

            return summary;
        }

        internal static string BuildContentJsonForXml(byte[] xmlBytes, string relativePath)
        {
            var sections = ParseSectionsFromXml(xmlBytes);
            var sourceFiles = new
            {
                complete = new
                {
                    relative_path = relativePath,
                    format = "xml"
                }
            };

            var contentJson = new
            {
                sections = sections,
                source = sourceFiles
            };

            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            return SanitizeJson(serializer.Serialize(contentJson));
        }

        private static List<ReleaseFileSelection> SelectReleaseFilesByRelease(List<ReleaseFileRow> releaseFiles)
        {
            var grouped = releaseFiles
                .Where(r => !string.IsNullOrWhiteSpace(r.relative_path))
                .GroupBy(r => r.release_number ?? string.Empty)
                .Select(g =>
                {
                    var xmlFiles = g
                        .Where(r => r.relative_path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    var completeXml = xmlFiles.FirstOrDefault(r =>
                        r.relative_path.IndexOf("_Complete_", StringComparison.OrdinalIgnoreCase) >= 0);

                    var releaseXml = xmlFiles.FirstOrDefault(r =>
                        r.relative_path.IndexOf("_R", StringComparison.OrdinalIgnoreCase) >= 0);

                    var selectedXml = completeXml ?? releaseXml ?? xmlFiles.FirstOrDefault();

                    return new ReleaseFileSelection
                    {
                        ReleaseNumber = g.Key,
                        Complete = selectedXml == null ? null : new ReleaseFileInfo
                        {
                            XmlPath = selectedXml?.relative_path
                        }
                    };
                })
                .Where(x => x.Complete != null)
                .OrderByDescending(x => ParseReleaseNumber(x.ReleaseNumber))
                .ToList();

            return grouped;
        }

        private static int ParseReleaseNumber(string releaseNumber)
        {
            if (int.TryParse(releaseNumber, out var value))
            {
                return value;
            }
            return 0;
        }

        private static async Task<byte[]> DownloadFileBytes(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                throw new ArgumentException("Relative path is required.");
            }

            var trimmed = relativePath.TrimStart('/');
            var url = TrainingComponentFilesBaseUrl + trimmed;

            using (var httpClient = new HttpClient())
            {
                var response = await httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Failed to download {url}. Status: {response.StatusCode}");
                }
                return await response.Content.ReadAsByteArrayAsync();
            }
        }

        private static List<string> ExtractLinesFromPdf(byte[] pdfBytes)
        {
            using (var stream = new MemoryStream(pdfBytes))
            using (var document = PdfDocument.Open(stream))
            {
                var lines = new List<string>();
                foreach (var page in document.GetPages())
                {
                    var text = page.Text ?? string.Empty;
                    var pageLines = text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
                    lines.AddRange(pageLines);
                }
                return lines;
            }
        }

        private static async Task<LoadedLinesResult> LoadLinesXmlOnly(ReleaseFileInfo fileInfo)
        {
            if (fileInfo == null)
            {
                throw new ArgumentNullException(nameof(fileInfo));
            }

            if (!string.IsNullOrWhiteSpace(fileInfo.XmlPath))
            {
                var xmlBytes = await DownloadFileBytes(fileInfo.XmlPath);
                var xmlLines = ExtractLinesFromXml(xmlBytes);
                return new LoadedLinesResult(xmlLines, "xml", fileInfo.XmlPath, xmlBytes);
            }

            return new LoadedLinesResult(new List<string>(), "xml", null, null);
        }

        private static List<string> ExtractLinesFromXml(byte[] xmlBytes)
        {
            using (var stream = new MemoryStream(xmlBytes))
            {
                var doc = XDocument.Load(stream);

                var fallback = new List<string>();
                foreach (var node in doc.DescendantNodes().OfType<XText>())
                {
                    var text = node.Value ?? string.Empty;
                    var split = text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
                    foreach (var part in split)
                    {
                        var trimmed = part.Trim();
                        if (!string.IsNullOrWhiteSpace(trimmed))
                        {
                            fallback.Add(trimmed);
                        }
                    }
                }

                return fallback;
            }
        }

        private static List<DocumentSection> ParseSectionsFromXml(byte[] xmlBytes)
        {
            if (xmlBytes == null || xmlBytes.Length == 0)
            {
                return new List<DocumentSection>();
            }

            using (var stream = new MemoryStream(xmlBytes))
            {
                var doc = XDocument.Load(stream);
                var ns = doc.Root != null ? doc.Root.Name.Namespace : XNamespace.None;

                var sections = new List<DocumentSection>();
                var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var order = 1;

                foreach (var topic in doc.Descendants(ns + "Topic"))
                {
                    var title = topic.Element(ns + "Headings")?.Element(ns + "PrintHeading")?.Value;
                    if (string.IsNullOrWhiteSpace(title))
                    {
                        continue;
                    }

                    var key = NormalizeKey(title);
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        continue;
                    }

                    if (seenKeys.Contains(key))
                    {
                        continue;
                    }

                    seenKeys.Add(key);
                    sections.Add(ParseTopicToSection(topic, ns, key, title.Trim(), order++));
                }

                return sections;
            }
        }

        private static DocumentSection ParseTopicToSection(XElement topic, XNamespace ns, string sectionKey, string sectionTitle, int order)
        {
            var section = new DocumentSection
            {
                key = sectionKey,
                title = sectionTitle,
                order = order
            };

            if (IsElementsPerformanceCriteriaSection(sectionKey))
            {
                section.table = ParseElementsTableFromXml(topic, ns);
                return section;
            }

            var textNode = topic.Element(ns + "Text");
            if (textNode != null)
            {
                var items = new List<SectionItem>();
                SectionTable table = null;
                var itemOrder = 1;
                var bulletStack = new Stack<SectionItem>();
                string lastParagraphItemId = null;

                foreach (var node in textNode.Elements())
                {
                    if (node.Name == ns + "table")
                    {
                        if (GetTableMaxColumnsFromTable(node, ns) > 1 && GetTableRowCountFromTable(node, ns) > 1)
                        {
                            table = ParseGenericTableFromXmlTable(node, ns, sectionKey);
                            table.order = itemOrder++;
                        }
                        else
                        {
                            foreach (var p in node.Descendants(ns + "p"))
                            {
                                AddParagraphItem(items, p, sectionKey, ref itemOrder, bulletStack, ref lastParagraphItemId);
                            }
                        }
                        continue;
                    }

                    if (node.Name == ns + "p")
                    {
                        AddParagraphItem(items, node, sectionKey, ref itemOrder, bulletStack, ref lastParagraphItemId);
                    }
                }

                section.items = items;
                section.table = table;

                if (section.items.Count > 0 || section.table != null)
                {
                    return section;
                }
            }

            var fallbackTextLines = ExtractTextLinesFromTopic(topic, ns);
            section.items = ParseItems(fallbackTextLines, sectionKey);
            return section;
        }

        private static List<string> ExtractTextLinesFromTopic(XElement topic, XNamespace ns)
        {
            var textNode = topic.Element(ns + "Text");
            if (textNode == null)
            {
                return new List<string>();
            }

            var lines = new List<string>();
            foreach (var p in textNode.Descendants(ns + "p"))
            {
                var text = ExtractInlineText(p);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    lines.Add(text.Trim());
                }
            }

            if (lines.Count > 0)
            {
                return lines;
            }

            var fallback = ExtractInlineText(textNode);
            return string.IsNullOrWhiteSpace(fallback) ? new List<string>() : new List<string> { fallback.Trim() };
        }

        private static List<SectionItem> ParseItemsFromXmlTopic(XElement topic, XNamespace ns, string sectionKey)
        {
            var textNode = topic.Element(ns + "Text");
            if (textNode == null)
            {
                return new List<SectionItem>();
            }
            return ParseItemsFromXmlParagraphs(textNode.Descendants(ns + "p"), sectionKey);
        }

        private static List<SectionItem> ParseItemsFromXmlParagraphs(IEnumerable<XElement> paragraphs, string sectionKey)
        {
            var items = new List<SectionItem>();
            var bulletStack = new Stack<SectionItem>();
            string lastParagraphItemId = null;
            var order = 1;

            foreach (var p in paragraphs)
            {
                var text = ExtractInlineText(p);
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                var idAttr = p.Attribute("id")?.Value;
                var indent = 0;
                var isBullet = false;

                if (int.TryParse(idAttr, out var idValue))
                {
                    if (idValue >= 31016)
                    {
                        isBullet = true;
                        indent = 2;
                    }
                    else if (idValue >= 14)
                    {
                        isBullet = true;
                        indent = 1;
                    }
                    else if (idValue >= 13)
                    {
                        isBullet = true;
                        indent = 0;
                    }
                }

                var item = new SectionItem
                {
                    item_id = $"{sectionKey}-{order}",
                    type = isBullet ? "bullet" : "paragraph",
                    text = text.Trim(),
                    order = order++,
                    indent = isBullet ? (int?)indent : null
                };
                if (isBullet)
                {
                    item.parent_bullet_item_id = GetParentBulletIdForXml(bulletStack, indent, lastParagraphItemId);
                    UpdateBulletStack(bulletStack, item);
                }
                else
                {
                    lastParagraphItemId = item.item_id;
                }

                items.Add(item);
            }

            return items;
        }

        private static SectionTable ParseElementsTableFromXml(XElement topic, XNamespace ns)
        {
            var table = topic.Element(ns + "Text")?.Element(ns + "table");
            var rows = new List<ElementCriteriaRow>();
            if (table == null)
            {
                return new SectionTable { columns = new List<string> { "Element", "Performance Criteria" }, rows = new List<TableRowBase>() };
            }

            ElementCriteriaRow currentRow = null;

            foreach (var tr in table.Elements(ns + "tr"))
            {
                var tds = tr.Elements(ns + "td").ToList();
                if (tds.Count < 2)
                {
                    continue;
                }

                var cellTexts = tds.Select(ExtractInlineText).Select(t => t.Trim()).ToList();

                string elementNo = null;
                string elementText = null;
                string criteriaNo = null;
                string criteriaText = null;

                if (cellTexts.Any(t => string.Equals(t, "ELEMENTS", StringComparison.OrdinalIgnoreCase)) ||
                    cellTexts.Any(t => string.Equals(t, "ELEMENT", StringComparison.OrdinalIgnoreCase)) ||
                    cellTexts.Any(t => string.Equals(t, "Element", StringComparison.OrdinalIgnoreCase)) ||
                    cellTexts.Any(t => string.Equals(t, "PERFORMANCE CRITERIA", StringComparison.OrdinalIgnoreCase)) ||
                    cellTexts.Any(t => string.Equals(t, "Performance Criteria", StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrWhiteSpace(cellTexts.ElementAtOrDefault(0)) && cellTexts[0].StartsWith("Elements describe", StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrWhiteSpace(cellTexts.ElementAtOrDefault(1)) && cellTexts[1].StartsWith("Performance criteria describe", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                if (tds.Count == 2)
                {
                    var firstCellText = cellTexts[0];
                    var criteriaOnlyMatch = Regex.Match(firstCellText, @"^\s*(\d+\.\d+)\s*\.?\s*$");
                    if (criteriaOnlyMatch.Success)
                    {
                        criteriaNo = firstCellText;
                        criteriaText = cellTexts[1];
                    }
                    else
                    {
                        var elementCellText = firstCellText;
                        var elementMatch = Regex.Match(elementCellText, @"^\s*(\d+)\s*\.?\s*(.+)$");
                        var elementNumber = elementMatch.Success ? elementMatch.Groups[1].Value : (rows.Count + 1).ToString();
                        var elementDisplayText = elementMatch.Success ? elementMatch.Groups[2].Value.Trim() : elementCellText;

                        currentRow = new ElementCriteriaRow
                        {
                            element_id = $"element-{elementNumber.Replace(".", "_").Replace("-", "_")}",
                            element_text = string.IsNullOrWhiteSpace(elementDisplayText)
                                ? elementNumber
                                : $"{elementNumber} {elementDisplayText}".Trim(),
                            criteria = new List<CriteriaItem>()
                        };
                        rows.Add(currentRow);

                        var criteriaParagraphs = tds[1]
                            .Descendants(ns + "p")
                            .Select(ExtractInlineText)
                            .Where(t => !string.IsNullOrWhiteSpace(t))
                            .ToList();

                        foreach (var criteriaLine in criteriaParagraphs)
                        {
                            var criteriaMatch = Regex.Match(criteriaLine, @"^\s*(\d+\.\d+)\.?\s*(.+)$");
                            var criteriaNumber = criteriaMatch.Success
                                ? criteriaMatch.Groups[1].Value
                                : $"{elementNumber}.{currentRow.criteria.Count + 1}";
                            var criteriaDisplayText = criteriaMatch.Success ? criteriaMatch.Groups[2].Value.Trim() : criteriaLine.Trim();

                            if (!string.IsNullOrWhiteSpace(criteriaDisplayText) &&
                                criteriaDisplayText.StartsWith(criteriaNumber + " ", StringComparison.Ordinal))
                            {
                                criteriaDisplayText = criteriaDisplayText.Substring(criteriaNumber.Length).Trim();
                            }

                            currentRow.criteria.Add(new CriteriaItem
                            {
                                id = $"criteria-{criteriaNumber.Replace(".", "_").Replace("-", "_")}",
                                text = string.IsNullOrWhiteSpace(criteriaDisplayText)
                                    ? criteriaNumber
                                    : $"{criteriaNumber} {criteriaDisplayText}"
                            });
                        }

                        continue;
                    }
                }

                if (tds.Count >= 4)
                {
                    elementNo = cellTexts[0];
                    elementText = cellTexts[1];
                    criteriaNo = cellTexts[2];
                    criteriaText = cellTexts[3];
                }
                else
                {
                    // Rowspanned element columns omitted; criteria columns remain
                    criteriaNo = cellTexts[0];
                    criteriaText = cellTexts[1];
                }

                if (!string.IsNullOrWhiteSpace(elementNo) || !string.IsNullOrWhiteSpace(elementText))
                {
                    var elementNumber = elementNo;
                    if (string.IsNullOrWhiteSpace(elementNumber))
                    {
                        var match = Regex.Match(elementText, @"^\d+");
                        elementNumber = match.Success ? match.Value : (rows.Count + 1).ToString();
                    }

                    var elementDisplayText = elementText;
                    if (string.IsNullOrWhiteSpace(elementDisplayText) && !string.IsNullOrWhiteSpace(elementNo))
                    {
                        elementDisplayText = elementNo;
                    }

                    currentRow = new ElementCriteriaRow
                    {
                        element_id = $"element-{elementNumber.Replace(".", "_").Replace("-", "_")}",
                        element_text = string.IsNullOrWhiteSpace(elementDisplayText)
                            ? elementNumber
                            : $"{elementNumber} {elementDisplayText}",
                        criteria = new List<CriteriaItem>()
                    };
                    rows.Add(currentRow);
                }

                if (!string.IsNullOrWhiteSpace(criteriaText) || !string.IsNullOrWhiteSpace(criteriaNo))
                {
                    if (currentRow == null)
                    {
                        currentRow = new ElementCriteriaRow
                        {
                            element_id = "element-unknown",
                            element_text = "Unknown",
                            criteria = new List<CriteriaItem>()
                        };
                        rows.Add(currentRow);
                    }

                    var criteriaNumber = criteriaNo;
                    if (string.IsNullOrWhiteSpace(criteriaNumber))
                    {
                        var match = Regex.Match(criteriaText, @"^\d+\.\d+");
                        criteriaNumber = match.Success ? match.Value : $"{currentRow.criteria.Count + 1}";
                    }

                    var criteriaDisplayText = criteriaText;
                    if (!string.IsNullOrWhiteSpace(criteriaDisplayText) && criteriaDisplayText.StartsWith(criteriaNumber + " ", StringComparison.Ordinal))
                    {
                        criteriaDisplayText = criteriaDisplayText.Substring(criteriaNumber.Length).Trim();
                    }
                    currentRow.criteria.Add(new CriteriaItem
                    {
                        id = $"criteria-{criteriaNumber.Replace(".", "_").Replace("-", "_")}",
                        text = string.IsNullOrWhiteSpace(criteriaDisplayText)
                            ? criteriaNumber
                            : $"{criteriaNumber} {criteriaDisplayText}"
                    });
                }
            }

            return new SectionTable
            {
                columns = new List<string> { "Element", "Performance Criteria" },
                rows = rows.Cast<TableRowBase>().ToList()
            };
        }

        private static SectionTable ParseGenericTableFromXmlTable(XElement table, XNamespace ns, string sectionKey)
        {
            var rows = new List<TableRowBase>();
            if (table == null)
            {
                return new SectionTable { columns = new List<string>(), rows = rows };
            }

            var tableRows = table.Elements(ns + "tr").ToList();
            if (tableRows.Count == 0)
            {
                return new SectionTable { columns = new List<string>(), rows = rows };
            }

            var headerIndex = -1;
            for (var i = 0; i < tableRows.Count; i++)
            {
                var headerAttr = tableRows[i].Attribute("header")?.Value;
                if (string.Equals(headerAttr, "true", StringComparison.OrdinalIgnoreCase))
                {
                    headerIndex = i;
                    break;
                }
            }

            var maxColumns = 0;
            foreach (var tr in tableRows)
            {
                var tdCount = tr.Elements(ns + "td")
                    .Select(td => GetSpanValue(td, "colspan"))
                    .Sum();
                if (tdCount > maxColumns)
                {
                    maxColumns = tdCount;
                }
            }

            var columns = new List<string>();
            if (headerIndex >= 0)
            {
                var headerCells = tableRows[headerIndex]
                    .Elements(ns + "td")
                    .ToList();

                foreach (var td in headerCells)
                {
                    var text = ExtractInlineText(td).Trim();
                    var span = GetSpanValue(td, "colspan");
                    if (span < 1)
                    {
                        span = 1;
                    }

                    columns.Add(string.IsNullOrWhiteSpace(text) ? $"Column {columns.Count + 1}" : text);
                    for (var i = 1; i < span; i++)
                    {
                        columns.Add($"Column {columns.Count + 1}");
                    }
                }
            }

            if (columns.Count == 0)
            {
                for (var i = 1; i <= Math.Max(1, maxColumns); i++)
                {
                    columns.Add($"Column {i}");
                }
            }

            var activeRowSpans = new List<RowSpanCell>();
            for (var i = 0; i < columns.Count; i++)
            {
                activeRowSpans.Add(null);
            }

            for (var i = 0; i < tableRows.Count; i++)
            {
                if (i == headerIndex)
                {
                    continue;
                }

                var cells = tableRows[i]
                    .Elements(ns + "td")
                    .ToList();

                if (cells.All(td => string.IsNullOrWhiteSpace(ExtractInlineText(td))))
                {
                    continue;
                }

                var rowCells = new List<List<SectionItem>>();
                for (var colIndex = 0; colIndex < columns.Count; colIndex++)
                {
                    rowCells.Add(null);
                }

                var nextCellIndex = 0;
                var rowIndex = rows.Count + 1;

                // Fill cells, respecting rowspans from previous rows.
                for (var colIndex = 0; colIndex < columns.Count; colIndex++)
                {
                    if (activeRowSpans[colIndex] == null)
                    {
                        continue;
                    }

                    rowCells[colIndex] = activeRowSpans[colIndex].Items;
                    activeRowSpans[colIndex].RemainingRows--;
                    if (activeRowSpans[colIndex].RemainingRows <= 0)
                    {
                        activeRowSpans[colIndex] = null;
                    }
                }

                for (var cellIndex = 0; cellIndex < cells.Count; cellIndex++)
                {
                    while (nextCellIndex < columns.Count && rowCells[nextCellIndex] != null)
                    {
                        nextCellIndex++;
                    }

                    if (nextCellIndex >= columns.Count)
                    {
                        break;
                    }

                    var cell = cells[cellIndex];
                    var cellItems = ParseCellItemsFromTd(cell, ns, sectionKey, rowIndex, nextCellIndex + 1);
                    var colspan = GetSpanValue(cell, "colspan");
                    if (colspan < 1)
                    {
                        colspan = 1;
                    }

                    for (var spanOffset = 0; spanOffset < colspan && (nextCellIndex + spanOffset) < columns.Count; spanOffset++)
                    {
                        rowCells[nextCellIndex + spanOffset] = spanOffset == 0
                            ? cellItems
                            : new List<SectionItem>();
                    }

                    var rowspan = GetSpanValue(cell, "rowspan");
                    if (rowspan > 1)
                    {
                        for (var spanOffset = 0; spanOffset < colspan && (nextCellIndex + spanOffset) < columns.Count; spanOffset++)
                        {
                            activeRowSpans[nextCellIndex + spanOffset] = new RowSpanCell(
                                spanOffset == 0 ? cellItems : new List<SectionItem>(),
                                rowspan - 1);
                        }
                    }

                    nextCellIndex += colspan;
                }

                rows.Add(new GenericTableRow { cells = rowCells });
            }

            return new SectionTable
            {
                columns = columns,
                rows = rows
            };
        }

        private sealed class RowSpanCell
        {
            public RowSpanCell(List<SectionItem> items, int remainingRows)
            {
                Items = items;
                RemainingRows = remainingRows;
            }

            public List<SectionItem> Items { get; }
            public int RemainingRows { get; set; }
        }

        private static int GetSpanValue(XElement td, string attributeName)
        {
            if (td == null)
            {
                return 1;
            }

            var value = td.Attribute(attributeName)?.Value;
            return int.TryParse(value, out var span) && span > 0 ? span : 1;
        }

        private static string ExtractInlineText(XElement element)
        {
            if (element == null)
            {
                return string.Empty;
            }

            var text = string.Concat(element.DescendantNodes().OfType<XText>().Select(t => t.Value));
            return Regex.Replace(text ?? string.Empty, @"\s+", " ").Trim();
        }

        private static string ExtractTitle(string code, List<string> lines)
        {
            foreach (var line in lines)
            {
                var trimmed = (line ?? string.Empty).Trim();
                if (trimmed.StartsWith(code + " ", StringComparison.OrdinalIgnoreCase))
                {
                    return trimmed.Substring(code.Length).Trim();
                }
            }
            return null;
        }

        private static string ExtractGeneratedDate(List<string> lines)
        {
            var match = lines
                .Select(l => l ?? string.Empty)
                .Select(l => l.Trim())
                .FirstOrDefault(l => l.IndexOf("Date this document was generated:", StringComparison.OrdinalIgnoreCase) >= 0);

            if (string.IsNullOrEmpty(match))
            {
                return null;
            }

            var parts = match.Split(new[] { "Date this document was generated:" }, StringSplitOptions.None);
            if (parts.Length < 2)
            {
                return null;
            }

            var tail = parts[1].Trim();
            if (string.IsNullOrEmpty(tail))
            {
                return null;
            }

            // Extract the first "d MMMM yyyy" date from the tail.
            var dateMatch = Regex.Match(tail, @"\b\d{1,2}\s+[A-Za-z]+\s+\d{4}\b");
            return dateMatch.Success ? dateMatch.Value : tail;
        }

        private static List<DocumentSection> MergeSections(List<DocumentSection> primary, List<DocumentSection> secondary)
        {
            var merged = new List<DocumentSection>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var order = 1;

            void AddSection(DocumentSection section)
            {
                if (section == null || string.IsNullOrWhiteSpace(section.key))
                {
                    return;
                }

                if (seen.Contains(section.key))
                {
                    return;
                }

                seen.Add(section.key);
                section.order = order++;
                merged.Add(section);
            }

            foreach (var section in primary)
            {
                AddSection(section);
            }

            foreach (var section in secondary)
            {
                AddSection(section);
            }

            return merged;
        }

        private static List<SectionItem> ParseItems(List<string> lines, string sectionKey)
        {
            var items = new List<SectionItem>();
            SectionItem lastItem = null;
            var bulletStack = new Stack<SectionItem>();
            var order = 1;

            foreach (var rawLine in lines)
            {
                var line = rawLine ?? string.Empty;
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (IsBulletLine(line, out var bulletText, out var indent))
                {
                    if (lastItem != null && lastItem.type == "bullet" && lastItem.text.EndsWith(":"))
                    {
                        indent = Math.Max(indent, (lastItem.indent ?? 0) + 1);
                    }

                    var item = new SectionItem
                    {
                        item_id = $"{sectionKey}-{order}",
                        type = "bullet",
                        text = bulletText,
                        order = order++,
                        indent = indent
                    };
                    item.parent_bullet_item_id = GetParentBulletId(bulletStack, indent);
                    items.Add(item);
                    lastItem = item;
                    UpdateBulletStack(bulletStack, item);
                    continue;
                }

                if (lastItem != null && lastItem.type == "bullet")
                {
                    lastItem.text = $"{lastItem.text} {line.Trim()}";
                    continue;
                }

                var paragraph = new SectionItem
                {
                    item_id = $"{sectionKey}-{order}",
                    type = "paragraph",
                    text = line.Trim(),
                    order = order++
                };
                items.Add(paragraph);
                lastItem = paragraph;
            }

            return items;
        }

        private static bool IsBulletLine(string line, out string text, out int indent)
        {
            text = null;
            indent = 0;

            if (string.IsNullOrWhiteSpace(line))
            {
                return false;
            }

            var bulletChars = new[] { '•', '·', '•', '' };
            var index = line.IndexOfAny(bulletChars);
            if (index < 0)
            {
                return false;
            }

            var leadingSpaces = line.Take(index).Count(char.IsWhiteSpace);
            indent = leadingSpaces >= 4 ? 2 : (leadingSpaces >= 2 ? 1 : 0);

            var extracted = line.Substring(index + 1).Trim();
            if (string.IsNullOrWhiteSpace(extracted))
            {
                return false;
            }

            text = extracted;
            return true;
        }

        private static SectionTable ParseElementsTable(List<string> lines)
        {
            var rows = new List<ElementCriteriaRow>();
            var combined = string.Join(" ", lines.Select(l => (l ?? string.Empty).Trim()));
            combined = Regex.Replace(combined, @"\s+", " ").Trim();
            // Normalize spaced criteria numbers like "1 . 1" -> "1.1"
            combined = Regex.Replace(combined, @"(\d+)\s*\.\s*(\d+)", "$1.$2");

            if (string.IsNullOrWhiteSpace(combined))
            {
                return new SectionTable { columns = new List<string> { "Element", "Performance Criteria" }, rows = new List<TableRowBase>() };
            }

            var criteriaRegex = new Regex(@"\b(\d+)\.(\d+)\b");
            var criteriaMatches = criteriaRegex.Matches(combined).Cast<Match>().ToList();

            if (criteriaMatches.Count == 0)
            {
                return new SectionTable { columns = new List<string> { "Element", "Performance Criteria" }, rows = new List<TableRowBase>() };
            }

            var criteriaItems = new List<(int ElementNumber, string Text, int StartIndex)>();
            for (var i = 0; i < criteriaMatches.Count; i++)
            {
                var start = criteriaMatches[i].Index;
                var end = i + 1 < criteriaMatches.Count ? criteriaMatches[i + 1].Index : combined.Length;
                var text = combined.Substring(start, end - start).Trim();

                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                if (!int.TryParse(criteriaMatches[i].Groups[1].Value, out var elementNo))
                {
                    continue;
                }

                criteriaItems.Add((elementNo, text, start));
            }

            var elementNumbers = criteriaItems.Select(c => c.ElementNumber).Distinct().OrderBy(n => n).ToList();
            foreach (var elementNumber in elementNumbers)
            {
                var firstCriteria = criteriaItems.First(c => c.ElementNumber == elementNumber);
                var elementText = ExtractElementText(combined, elementNumber, firstCriteria.StartIndex);
                if (string.IsNullOrWhiteSpace(elementText))
                {
                    elementText = elementNumber.ToString();
                }

                var row = new ElementCriteriaRow
                {
                    element_id = $"element-{elementNumber.ToString().Replace(".", "_").Replace("-", "_")}",
                    element_text = elementText,
                    criteria = new List<CriteriaItem>()
                };

                foreach (var criteria in criteriaItems.Where(c => c.ElementNumber == elementNumber))
                {
                    var criteriaNumberMatch = Regex.Match(criteria.Text, @"^\d+\.\d+");
                    var criteriaNumber = criteriaNumberMatch.Success ? criteriaNumberMatch.Value : $"{elementNumber}.{row.criteria.Count + 1}";
                    row.criteria.Add(new CriteriaItem
                    {
                        id = $"criteria-{criteriaNumber.Replace(".", "_").Replace("-", "_")}",
                        text = criteria.Text
                    });
                }

                rows.Add(row);
            }

            return new SectionTable
            {
                columns = new List<string> { "Element", "Performance Criteria" },
                rows = rows.Cast<TableRowBase>().ToList()
            };
        }

        private static string ExtractElementText(string combined, int elementNumber, int criteriaStartIndex)
        {
            var searchStart = Math.Max(0, criteriaStartIndex - 800);
            var window = combined.Substring(searchStart, criteriaStartIndex - searchStart);
            var elementRegex = new Regex(@"\b" + elementNumber + @"\s+[A-Za-z][^0-9]*", RegexOptions.RightToLeft);
            var match = elementRegex.Match(window);
            return match.Success ? match.Value.Trim() : null;
        }

        private sealed class ReleaseFileSelection
        {
            public string ReleaseNumber { get; set; }
            public ReleaseFileInfo Complete { get; set; }
        }

        private sealed class ReleaseFileInfo
        {
            public string XmlPath { get; set; }
        }

        private sealed class LoadedLinesResult
        {
            public LoadedLinesResult(List<string> lines, string formatUsed, string selectedRelativePath, byte[] bytes)
            {
                Lines = lines ?? new List<string>();
                FormatUsed = formatUsed;
                SelectedRelativePath = selectedRelativePath;
                Bytes = bytes;
            }

            public List<string> Lines { get; }
            public string FormatUsed { get; }
            public string SelectedRelativePath { get; }
            public byte[] Bytes { get; }
        }

        private static string NormalizeKey(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            var lowered = input.Trim().ToLowerInvariant();
            var normalized = lowered
                .Replace('-', '_')
                .Replace('/', '_')
                .Replace('\\', '_')
                .Replace(' ', '_');

            var cleaned = new string(normalized.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
            while (cleaned.Contains("__"))
            {
                cleaned = cleaned.Replace("__", "_");
            }

            return cleaned.Trim('_');
        }

        private static bool IsBulletSection(string sectionKey)
        {
            return SectionKeyEquals(sectionKey, "performance_evidence") ||
                   SectionKeyEquals(sectionKey, "knowledge_evidence");
        }

        private static bool IsElementsPerformanceCriteriaSection(string sectionKey)
        {
            return SectionKeyEquals(sectionKey, "elements_and_performance_criteria") ||
                   SectionKeyEquals(sectionKey, "elements_performance_criteria");
        }

        private static bool SectionKeyEquals(string left, string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasBulletParagraphs(XElement topic, XNamespace ns)
        {
            var textNode = topic.Element(ns + "Text");
            if (textNode == null)
            {
                return false;
            }

            return HasBulletParagraphs(textNode.Descendants(ns + "p"));
        }

        private static bool HasBulletParagraphs(IEnumerable<XElement> paragraphs)
        {
            foreach (var p in paragraphs)
            {
                var idAttr = p.Attribute("id")?.Value;
                if (int.TryParse(idAttr, out var idValue) && idValue >= 13)
                {
                    return true;
                }
            }

            return false;
        }

        private static List<XElement> GetParagraphsOutsideTables(XElement topic, XNamespace ns)
        {
            var textNode = topic.Element(ns + "Text");
            if (textNode == null)
            {
                return new List<XElement>();
            }

            return textNode
                .Descendants(ns + "p")
                .Where(p => !p.Ancestors(ns + "table").Any())
                .ToList();
        }

        private static bool HasTable(XElement topic, XNamespace ns)
        {
            return topic.Element(ns + "Text")?.Element(ns + "table") != null;
        }

        private static int GetTableMaxColumns(XElement topic, XNamespace ns)
        {
            var table = topic.Element(ns + "Text")?.Element(ns + "table");
            return GetTableMaxColumnsFromTable(table, ns);
        }

        private static int GetTableMaxColumnsFromTable(XElement table, XNamespace ns)
        {
            if (table == null)
            {
                return 0;
            }

            var maxColumns = 0;
            foreach (var tr in table.Elements(ns + "tr"))
            {
                var tdCount = tr.Elements(ns + "td").Count();
                if (tdCount > maxColumns)
                {
                    maxColumns = tdCount;
                }
            }

            return maxColumns;
        }

        private static int GetTableRowCount(XElement topic, XNamespace ns)
        {
            var table = topic.Element(ns + "Text")?.Element(ns + "table");
            return GetTableRowCountFromTable(table, ns);
        }

        private static int GetTableRowCountFromTable(XElement table, XNamespace ns)
        {
            if (table == null)
            {
                return 0;
            }

            return table.Elements(ns + "tr").Count();
        }

        private static void AddParagraphItem(
            List<SectionItem> items,
            XElement p,
            string sectionKey,
            ref int order,
            Stack<SectionItem> bulletStack,
            ref string lastParagraphItemId)
        {
            var text = ExtractInlineText(p);
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            var idAttr = p.Attribute("id")?.Value;
            var indent = 0;
            var isBullet = false;

            if (int.TryParse(idAttr, out var idValue))
            {
                if (idValue >= 31016)
                {
                    isBullet = true;
                    indent = 2;
                }
                else if (idValue >= 14)
                {
                    isBullet = true;
                    indent = 1;
                }
                else if (idValue >= 13)
                {
                    isBullet = true;
                    indent = 0;
                }
            }

            var item = new SectionItem
            {
                item_id = $"{sectionKey}-{order}",
                type = isBullet ? "bullet" : "paragraph",
                text = text.Trim(),
                order = order++,
                indent = isBullet ? (int?)indent : null
            };
            if (isBullet)
            {
                item.parent_bullet_item_id = GetParentBulletIdForXml(bulletStack, indent, lastParagraphItemId);
                UpdateBulletStack(bulletStack, item);
            }
            else
            {
                lastParagraphItemId = item.item_id;
            }

            items.Add(item);
        }

        private static string SanitizeJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return json;
            }

            // Replace any undefined tokens with null
            return Regex.Replace(json, @"\bundefined\b", "null");
        }

        private sealed class DocumentSection
        {
            private readonly List<string> _contentLines = new List<string>();
            private readonly List<string> _tableLines = new List<string>();

            public string key { get; set; }
            public string title { get; set; }
            public int order { get; set; }
            public List<SectionItem> items { get; set; } = new List<SectionItem>();
            public SectionTable table { get; set; }

            public void AddContentLine(string line) => _contentLines.Add(line);
            public void AddTableLine(string line) => _tableLines.Add(line);
            public List<string> GetContentLines() => _contentLines;
            public List<string> GetTableLines() => _tableLines;
        }

        private sealed class SectionItem
        {
            public string item_id { get; set; }
            public string text { get; set; }
            public string type { get; set; }
            public int order { get; set; }
            public int? indent { get; set; }
            public string parent_bullet_item_id { get; set; }
        }

        private sealed class SectionTable
        {
            public List<string> columns { get; set; }
            public List<TableRowBase> rows { get; set; }
            public int? order { get; set; }
        }

        private abstract class TableRowBase
        {
        }

        private sealed class ElementCriteriaRow : TableRowBase
        {
            public string element_id { get; set; }
            public string element_text { get; set; }
            public List<CriteriaItem> criteria { get; set; }
        }

        private sealed class GenericTableRow : TableRowBase
        {
            public List<List<SectionItem>> cells { get; set; }
        }

        private static List<SectionItem> ParseCellItemsFromTd(
            XElement td,
            XNamespace ns,
            string sectionKey,
            int rowIndex,
            int columnIndex)
        {
            var items = new List<SectionItem>();
            var bulletStack = new Stack<SectionItem>();
            string lastParagraphItemId = null;
            var order = 1;

            var paragraphs = td.Descendants(ns + "p").ToList();
            if (paragraphs.Count == 0)
            {
                var text = ExtractInlineText(td);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    // item_id format: {key}_cell-{row}_{column}_{paragraphIndex}
                    var paragraphItem = new SectionItem
                    {
                        item_id = $"{sectionKey}_cell-{rowIndex}_{columnIndex}_{order}",
                        type = "paragraph",
                        text = text.Trim(),
                        order = order++,
                        indent = null
                    };
                    items.Add(paragraphItem);
                    lastParagraphItemId = paragraphItem.item_id;
                }
                return items;
            }

            foreach (var p in paragraphs)
            {
                var text = ExtractInlineText(p);
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                var idAttr = p.Attribute("id")?.Value;
                var indent = 0;
                var isBullet = false;

                if (int.TryParse(idAttr, out var idValue))
                {
                    if (idValue >= 31016)
                    {
                        isBullet = true;
                        indent = 2;
                    }
                    else if (idValue >= 14)
                    {
                        isBullet = true;
                        indent = 1;
                    }
                    else if (idValue >= 13)
                    {
                        isBullet = true;
                        indent = 0;
                    }
                }

                // item_id format: {key}_cell-{row}_{column}_{paragraphIndex}
                var cellItem = new SectionItem
                {
                    item_id = $"{sectionKey}_cell-{rowIndex}_{columnIndex}_{order}",
                    type = isBullet ? "bullet" : "paragraph",
                    text = text.Trim(),
                    order = order++,
                    indent = isBullet ? (int?)indent : null
                };
                if (isBullet)
                {
                    cellItem.parent_bullet_item_id = GetParentBulletIdForXml(bulletStack, indent, lastParagraphItemId);
                    UpdateBulletStack(bulletStack, cellItem);
                }
                else
                {
                    lastParagraphItemId = cellItem.item_id;
                }

                items.Add(cellItem);
            }

            return items;
        }

        private static string GetParentBulletId(Stack<SectionItem> bulletStack, int indent)
        {
            if (indent <= 0 || bulletStack.Count == 0)
            {
                return null;
            }

            var items = bulletStack.ToList();
            for (var i = items.Count - 1; i >= 0; i--)
            {
                var candidate = items[i];
                var candidateIndent = candidate.indent ?? 0;
                if (candidateIndent < indent)
                {
                    return candidate.item_id;
                }
            }

            return null;
        }

        private static string GetParentBulletIdForXml(
            Stack<SectionItem> bulletStack,
            int indent,
            string lastParagraphItemId)
        {
            if (indent <= 0)
            {
                return lastParagraphItemId;
            }

            return FindNearestBulletAtIndent(bulletStack, indent - 1);
        }

        private static string FindNearestBulletAtIndent(Stack<SectionItem> bulletStack, int targetIndent)
        {
            if (bulletStack == null || bulletStack.Count == 0)
            {
                return null;
            }

            var items = bulletStack.ToList();
            for (var i = items.Count - 1; i >= 0; i--)
            {
                var candidate = items[i];
                var candidateIndent = candidate.indent ?? 0;
                if (candidateIndent == targetIndent)
                {
                    return candidate.item_id;
                }
            }

            return null;
        }

        private static void UpdateBulletStack(Stack<SectionItem> bulletStack, SectionItem item)
        {
            var indent = item.indent ?? 0;
            while (bulletStack.Count > 0)
            {
                var topIndent = bulletStack.Peek().indent ?? 0;
                if (topIndent < indent)
                {
                    break;
                }
                bulletStack.Pop();
            }

            bulletStack.Push(item);
        }

        private sealed class CriteriaItem
        {
            public string id { get; set; }
            public string text { get; set; }
        }
    }
}
