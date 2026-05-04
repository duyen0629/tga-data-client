using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using TgaGateway2.Handlers.TrainingComponentDocuments.Helper;
using TgaGateway2.Services;
using TgaGateway2.Handlers.TrainingComponentDocuments;

namespace TgaGateway2
{
    public static class CSVTrainingCodeCheck
    {
        public static async Task RunAsync(
            SupabaseService supabaseService,
            string csvPath,
            string columnName = "training_code",
            bool hasHeader = true,
            bool processMissing = true,
            string logPath = null)
        {
            if (supabaseService == null)
            {
                throw new ArgumentNullException(nameof(supabaseService));
            }

            if (string.IsNullOrWhiteSpace(csvPath))
            {
                throw new ArgumentException("CSV path is required.", nameof(csvPath));
            }

            if (!File.Exists(csvPath))
            {
                throw new FileNotFoundException($"CSV file not found: {csvPath}");
            }

            var lines = File.ReadAllLines(csvPath);
            if (lines.Length == 0)
            {
                Console.WriteLine("CSV is empty.");
                return;
            }

            var startIndex = 0;
            var columnIndex = 0;
            if (hasHeader)
            {
                var header = ParseCsvLine(lines[0]);
                columnIndex = FindColumnIndex(header, columnName);
                if (columnIndex < 0)
                {
                    throw new Exception($"Column '{columnName}' not found in CSV header.");
                }
                startIndex = 1;
            }

            var codeCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var i = startIndex; i < lines.Length; i++)
            {
                var row = ParseCsvLine(lines[i]);
                if (columnIndex >= row.Count)
                {
                    continue;
                }

                var code = (row[columnIndex] ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(code))
                {
                    continue;
                }

                if (!codeCounts.ContainsKey(code))
                {
                    codeCounts[code] = 0;
                }
                codeCounts[code]++;
            }

            if (codeCounts.Count == 0)
            {
                Console.WriteLine("No training codes found in CSV.");
                return;
            }

            var totalCount = codeCounts.Values.Sum();
            var existsCount = 0;
            var notExistsCount = 0;
            var notExistsCodes = new List<string>();
            var logLines = new List<string>();

            var queryService = new SupabaseQueryService();
            var checkedCount = 0;
            foreach (var kvp in codeCounts)
            {
                var code = kvp.Key;
                var count = kvp.Value;
                var exists = await queryService.TrainingComponentDocumentExistsByCode(code);
                checkedCount++;
                Console.WriteLine($"Checked {checkedCount}/{codeCounts.Count} codes.");
                logLines.Add($"Checked {checkedCount}/{codeCounts.Count} codes.");
                if (exists)
                {
                    existsCount += count;
                }
                else
                {
                    notExistsCount += count;
                    notExistsCodes.Add(code);
                }
            }

            Console.WriteLine($"Total count: {totalCount}");
            Console.WriteLine($"Count exists: {existsCount}");
            Console.WriteLine($"Count not exists: {notExistsCount}");
            logLines.Add($"Total count: {totalCount}");
            logLines.Add($"Count exists: {existsCount}");
            logLines.Add($"Count not exists: {notExistsCount}");

            if (notExistsCodes.Count > 0)
            {
                Console.WriteLine("Codes not found:");
                logLines.Add("Codes not found:");
                foreach (var code in notExistsCodes.OrderBy(c => c))
                {
                    Console.WriteLine($" - {code}");
                    logLines.Add($" - {code}");
                }
            }

            if (processMissing && notExistsCodes.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("Processing missing codes...");
                logLines.Add(string.Empty);
                logLines.Add("Processing missing codes...");
                foreach (var code in notExistsCodes.OrderBy(c => c))
                {
                    await TrainingComponentDocumentHandler.ProcessTrainingComponentDocumentForCode(supabaseService, code);
                }
            }

            if (!string.IsNullOrWhiteSpace(logPath))
            {
                File.WriteAllLines(logPath, logLines);
                Console.WriteLine($"CSV check log saved to: {logPath}");
            }
        }

        public static async Task ExportLatestDocumentsToCsvAsync(
            SupabaseService supabaseService,
            string inputCsvPath,
            string outputCsvPath,
            string columnName = "training_code",
            bool hasHeader = true)
        {
            if (supabaseService == null)
            {
                throw new ArgumentNullException(nameof(supabaseService));
            }

            if (string.IsNullOrWhiteSpace(inputCsvPath))
            {
                throw new ArgumentException("CSV path is required.", nameof(inputCsvPath));
            }

            if (string.IsNullOrWhiteSpace(outputCsvPath))
            {
                throw new ArgumentException("Output CSV path is required.", nameof(outputCsvPath));
            }

            if (!File.Exists(inputCsvPath))
            {
                throw new FileNotFoundException($"CSV file not found: {inputCsvPath}");
            }

            var lines = File.ReadAllLines(inputCsvPath);
            if (lines.Length == 0)
            {
                Console.WriteLine("CSV is empty.");
                return;
            }

            var startIndex = 0;
            var columnIndex = 0;
            if (hasHeader)
            {
                var header = ParseCsvLine(lines[0]);
                columnIndex = FindColumnIndex(header, columnName);
                if (columnIndex < 0)
                {
                    throw new Exception($"Column '{columnName}' not found in CSV header.");
                }
                startIndex = 1;
            }

            var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = startIndex; i < lines.Length; i++)
            {
                var row = ParseCsvLine(lines[i]);
                if (columnIndex >= row.Count)
                {
                    continue;
                }

                var code = (row[columnIndex] ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(code))
                {
                    codes.Add(code);
                }
            }

            if (codes.Count == 0)
            {
                Console.WriteLine("No training codes found in CSV.");
                return;
            }

            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            var outputLines = new List<string>
            {
                "training_component_code,release_number,content_json"
            };

            var queryService = new SupabaseQueryService();
            var checkedCount = 0;
            foreach (var code in codes.OrderBy(c => c))
            {
                var row = await queryService.GetLatestTrainingComponentDocument(code);
                checkedCount++;
                Console.WriteLine($"Fetched latest document {checkedCount}/{codes.Count} for {code}.");

                var contentJson = row?.ContentJson != null ? serializer.Serialize(row.ContentJson) : string.Empty;
                var releaseNumber = row?.ReleaseNumber ?? string.Empty;
                var csvLine = string.Join(",",
                    ToCsvField(code),
                    ToCsvField(releaseNumber),
                    ToCsvField(contentJson));
                outputLines.Add(csvLine);
            }

            File.WriteAllLines(outputCsvPath, outputLines);
            Console.WriteLine($"Latest documents CSV saved to: {outputCsvPath}");
        }

        /// <summary>
        /// Reads a CSV with a training_code column, fetches release files from Supabase for each code,
        /// downloads the selected XML (latest release, prefer Complete then R), and saves each XML
        /// as {code}.xml in the xml folder.
        /// </summary>
        public static async Task ExportReleaseFilesXmlAsync(
            string inputCsvPath,
            string outputDirectory,
            string columnName = "training_code",
            bool hasHeader = true)
        {
            if (string.IsNullOrWhiteSpace(inputCsvPath))
            {
                throw new ArgumentException("Input CSV path is required.", nameof(inputCsvPath));
            }

            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                throw new ArgumentException("Output directory is required.", nameof(outputDirectory));
            }

            if (!File.Exists(inputCsvPath))
            {
                throw new FileNotFoundException($"Input CSV file not found: {inputCsvPath}");
            }

            var lines = File.ReadAllLines(inputCsvPath);
            if (lines.Length == 0)
            {
                Console.WriteLine("Input CSV is empty.");
                return;
            }

            var startIndex = 0;
            var columnIndex = 0;
            if (hasHeader)
            {
                var header = ParseCsvLine(lines[0]);
                columnIndex = FindColumnIndex(header, columnName);
                if (columnIndex < 0)
                {
                    throw new Exception($"Column '{columnName}' not found in CSV header.");
                }
                startIndex = 1;
            }

            var codes = new List<string>();
            for (var i = startIndex; i < lines.Length; i++)
            {
                var row = ParseCsvLine(lines[i]);
                if (columnIndex >= row.Count)
                {
                    continue;
                }

                var code = (row[columnIndex] ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(code))
                {
                    codes.Add(code);
                }
            }

            if (codes.Count == 0)
            {
                Console.WriteLine("No training codes found in input CSV.");
                return;
            }

            var xmlDir = Path.Combine(outputDirectory, "xml");
            Directory.CreateDirectory(xmlDir);

            var queryService = new SupabaseQueryService();
            var processedCount = 0;
            var notCreated = new List<string>();

            foreach (var code in codes)
            {
                processedCount++;
                Console.WriteLine($"Processing {processedCount}/{codes.Count}: {code}");

                try
                {
                    var releaseFiles = await queryService.GetReleaseFilesByCode(code);
                    if (releaseFiles == null || releaseFiles.Count == 0)
                    {
                        notCreated.Add($"{code} (no release files in database)");
                        Console.WriteLine($"  [NOT CREATED] {code}: No release files found in database.");
                        continue;
                    }

                    var candidates = ReleaseFileHelper.SelectReleaseFilesByRelease(releaseFiles);
                    if (candidates.Count == 0)
                    {
                        notCreated.Add($"{code} (no XML in release_files)");
                        Console.WriteLine($"  [NOT CREATED] {code}: No XML files in release_files.");
                        continue;
                    }

                    var candidate = candidates[0];
                    var safeCode = string.Join("_", (code ?? string.Empty).Split(Path.GetInvalidFileNameChars()));
                    var wroteAny = false;
                    for (var si = 0; si < candidate.XmlSources.Count; si++)
                    {
                        var loaded = await ReleaseFileHelper.LoadLinesXmlOnly(candidate.XmlSources[si]);
                        if (loaded.Bytes == null || loaded.Bytes.Length == 0)
                        {
                            continue;
                        }

                        var xmlFileName = si == 0 ? safeCode + ".xml" : safeCode + "_" + si + ".xml";
                        File.WriteAllBytes(Path.Combine(xmlDir, xmlFileName), loaded.Bytes);
                        wroteAny = true;
                    }

                    if (!wroteAny)
                    {
                        notCreated.Add($"{code} (XML download empty)");
                        Console.WriteLine($"  [NOT CREATED] {code}: XML download returned empty.");
                    }
                }
                catch (Exception ex)
                {
                    notCreated.Add($"{code} ({ex.Message})");
                    Console.WriteLine($"  [NOT CREATED] {code}: Failed - {ex.Message}");
                }
            }

            Console.WriteLine($"XML files saved to: {xmlDir}");
            if (notCreated.Count > 0)
            {
                Console.WriteLine($"Not created ({notCreated.Count}): {string.Join(", ", notCreated)}");
            }
        }

        private static int FindColumnIndex(IReadOnlyList<string> header, string columnName)
        {
            for (var i = 0; i < header.Count; i++)
            {
                if (string.Equals(header[i]?.Trim(), columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            return -1;
        }

        private static List<string> ParseCsvLine(string line)
        {
            var result = new List<string>();
            if (line == null)
            {
                return result;
            }

            var current = string.Empty;
            var inQuotes = false;

            for (var i = 0; i < line.Length; i++)
            {
                var ch = line[i];
                if (ch == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current += '"';
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                    continue;
                }

                if (ch == ',' && !inQuotes)
                {
                    result.Add(current);
                    current = string.Empty;
                    continue;
                }

                current += ch;
            }

            result.Add(current);
            return result;
        }

        private static string ToCsvField(string value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            var needsQuotes = value.IndexOfAny(new[] { ',', '"', '\n', '\r', '\t' }) >= 0;
            var escaped = value.Replace("\"", "\"\"");
            return needsQuotes ? $"\"{escaped}\"" : escaped;
        }
    }
}
