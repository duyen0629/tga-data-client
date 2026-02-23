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
    /// and selects which file to use per release.
    /// </summary>
    internal static class ReleaseFileHelper
    {
        private const string TrainingComponentFilesBaseUrl = "https://training.gov.au/TrainingComponentFiles/";

        /// <summary>
        /// Groups release files by release number and selects one XML per release (prefer Complete, then R, then first).
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
