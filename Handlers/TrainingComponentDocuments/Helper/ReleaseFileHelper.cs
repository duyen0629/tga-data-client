using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using TgaGateway2.Handlers.TrainingComponentDocuments.Type;
using TgaGateway2.Services;
using UglyToad.PdfPig;

namespace TgaGateway2.Handlers.TrainingComponentDocuments.Helper
{
    /// <summary>
    /// Loads release files (download, parse release number, extract lines from XML or PDF)
    /// and groups XML sources per release for merge.
    /// </summary>
    internal static class ReleaseFileHelper
    {
        private const string TrainingComponentFilesBaseUrl = "https://training.gov.au/TrainingComponentFiles/";

        /// <summary>
        /// Groups by release_number. If the group has any .xml whose path contains _Complete_, all those Complete .xml
        /// rows are selected (no _R{release_number} filter on Complete paths).
        /// If there is no Complete .xml, selects every .xml whose path matches _R{release_number}; if none match, all .xml in the group.
        /// Merge order within the picked set: _Complete_ first, then shorter path, then alphabetical.
        /// </summary>
        internal static List<ReleaseFileSelection> SelectReleaseFilesByRelease(List<ReleaseFileRow> releaseFiles)
        {
            if (releaseFiles == null) return new List<ReleaseFileSelection>();

            var grouped = releaseFiles
                .Where(r => !string.IsNullOrWhiteSpace(r.relative_path))
                .GroupBy(r => r.release_number ?? string.Empty)
                .Select(g =>
                {
                    var xmlFiles = g
                        .Where(r => r.relative_path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    var releaseKey = (g.Key ?? string.Empty).Trim();
                    var token = string.IsNullOrEmpty(releaseKey) ? null : "_R" + releaseKey;

                    var completeXmls = xmlFiles
                        .Where(r => r.relative_path.IndexOf("_Complete_", StringComparison.OrdinalIgnoreCase) >= 0)
                        .ToList();

                    List<ReleaseFileRow> picked;
                    if (completeXmls.Count > 0)
                    {
                        picked = completeXmls.ToList();
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(token))
                        {
                            picked = xmlFiles;
                        }
                        else
                        {
                            picked = xmlFiles
                                .Where(r => PathMatchesReleaseToken(r.relative_path, token))
                                .ToList();
                            if (picked.Count == 0)
                            {
                                picked = xmlFiles;
                            }
                        }
                    }

                    picked = DistinctReleaseFileRowsByPath(picked);
                    picked = OrderXmlRowsForMerge(picked);

                    return new ReleaseFileSelection
                    {
                        ReleaseNumber = g.Key,
                        XmlSources = picked
                            .Select(r => new ReleaseFileInfo { XmlPath = r.relative_path })
                            .ToList()
                    };
                })
                .Where(x => x.XmlSources != null && x.XmlSources.Count > 0)
                .OrderByDescending(x => ParseReleaseNumber(x.ReleaseNumber))
                .ToList();

            return grouped;
        }

        /// <summary>
        /// True when <paramref name="needle"/> appears as a release segment (followed by '.', '_', or end), not as a prefix of a longer release (e.g. _R1 vs _R10).
        /// </summary>
        internal static bool PathMatchesReleaseToken(string relativePath, string needle)
        {
            if (string.IsNullOrEmpty(relativePath) || string.IsNullOrEmpty(needle))
            {
                return true;
            }

            for (var start = 0; start < relativePath.Length;)
            {
                var i = relativePath.IndexOf(needle, start, StringComparison.OrdinalIgnoreCase);
                if (i < 0)
                {
                    return false;
                }

                var after = i + needle.Length;
                if (after >= relativePath.Length)
                {
                    return true;
                }

                var c = relativePath[after];
                if (c == '.' || c == '_')
                {
                    return true;
                }

                start = i + 1;
            }

            return false;
        }

        private static List<ReleaseFileRow> DistinctReleaseFileRowsByPath(List<ReleaseFileRow> rows)
        {
            return rows
                .GroupBy(r => (r.relative_path ?? string.Empty).TrimStart('/'), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();
        }

        private static List<ReleaseFileRow> OrderXmlRowsForMerge(List<ReleaseFileRow> rows)
        {
            return rows
                .OrderBy(r => r.relative_path.IndexOf("_Complete_", StringComparison.OrdinalIgnoreCase) >= 0 ? 0 : 1)
                .ThenBy(r => r.relative_path.Length)
                .ThenBy(r => r.relative_path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        internal static int ParseReleaseNumber(string releaseNumber)
        {
            if (int.TryParse(releaseNumber, out var value))
            {
                return value;
            }
            return 0;
        }

        internal static async Task<byte[]> DownloadFileBytes(string relativePath)
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

        internal static List<string> ExtractLinesFromPdf(byte[] pdfBytes)
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

        internal static async Task<LoadedLinesResult> LoadLinesXmlOnly(ReleaseFileInfo fileInfo)
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

        internal static List<string> ExtractLinesFromXml(byte[] xmlBytes)
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
    }
}
