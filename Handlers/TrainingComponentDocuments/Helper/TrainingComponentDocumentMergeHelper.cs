using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using TgaGateway2.Handlers.TrainingComponentDocuments.Parser;
using TgaGateway2.Handlers.TrainingComponentDocuments.Type;

namespace TgaGateway2.Handlers.TrainingComponentDocuments.Helper
{
    /// <summary>
    /// Helpers for loading ordered XML parts, deduplicating merged document sections,
    /// and building source/raw XML payloads for training component documents.
    /// </summary>
    internal static class TrainingComponentDocumentMergeHelper
    {
        internal static List<string> GetXmlSourcePaths(ReleaseFileSelection candidate)
        {
            if (candidate?.XmlSources == null)
            {
                return new List<string>();
            }

            return candidate.XmlSources
                .Where(s => s != null && !string.IsNullOrWhiteSpace(s.XmlPath))
                .Select(s => s.XmlPath)
                .ToList();
        }

        internal static async Task<List<(byte[] bytes, string relativePath)>> LoadOrderedXmlPartsAsync(
            ReleaseFileSelection candidate)
        {
            var list = new List<(byte[], string)>();
            if (candidate?.XmlSources == null)
            {
                return list;
            }

            foreach (var src in candidate.XmlSources)
            {
                var loaded = await ReleaseFileHelper.LoadLinesXmlOnly(src);
                list.Add((loaded.Bytes, loaded.SelectedRelativePath));
            }

            return list;
        }

        /// <summary>
        /// After concatenating sections from multiple XML sources, keeps first occurrence in order
        /// and drops later sections with the same <see cref="DocumentSection.key"/> or the same
        /// normalized <see cref="DocumentSection.title"/> (avoids duplicate sections in merged JSON).
        /// </summary>
        internal static List<DocumentSection> DeduplicateDocumentSections(IReadOnlyList<DocumentSection> sections)
        {
            if (sections == null || sections.Count == 0)
            {
                return new List<DocumentSection>();
            }

            var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenTitleNorm = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<DocumentSection>();

            foreach (var s in sections)
            {
                if (s == null)
                {
                    continue;
                }

                var duplicateKey = !string.IsNullOrWhiteSpace(s.key) && seenKeys.Contains(s.key);
                string titleNorm = null;
                if (!string.IsNullOrWhiteSpace(s.title))
                {
                    titleNorm = SectionKeyHelper.NormalizeKey(s.title);
                }

                var duplicateTitle = !string.IsNullOrWhiteSpace(titleNorm) && seenTitleNorm.Contains(titleNorm);
                if (duplicateKey || duplicateTitle)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(s.key))
                {
                    seenKeys.Add(s.key);
                }

                if (!string.IsNullOrWhiteSpace(titleNorm))
                {
                    seenTitleNorm.Add(titleNorm);
                }

                result.Add(s);
            }

            for (var i = 0; i < result.Count; i++)
            {
                result[i].order = i + 1;
            }

            return result;
        }

        internal static object BuildSourceFilesForOrderedXml(IReadOnlyList<(string path, string format)> ordered)
        {
            if (ordered == null || ordered.Count == 0)
            {
                return new { complete = new { relative_path = (string)null, format = "xml" } };
            }

            var head = ordered[0];
            if (ordered.Count == 1)
            {
                return new
                {
                    complete = new
                    {
                        relative_path = head.path,
                        format = head.format ?? "xml"
                    }
                };
            }

            return new
            {
                complete = new
                {
                    relative_path = head.path,
                    format = head.format ?? "xml"
                },
                additional_xml = ordered.Skip(1)
                    .Select(x => new { relative_path = x.path, format = x.format ?? "xml" })
                    .ToList()
            };
        }

        internal static string BuildSourceFilesJsonForError(IReadOnlyList<string> relativePaths)
        {
            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            object obj;
            if (relativePaths == null || relativePaths.Count == 0)
            {
                obj = new { complete = new { relative_path = (string)null, format = "xml" } };
            }
            else if (relativePaths.Count == 1)
            {
                obj = new { complete = new { relative_path = relativePaths[0], format = "xml" } };
            }
            else
            {
                obj = new
                {
                    complete = new { relative_path = relativePaths[0], format = "xml" },
                    additional_xml = relativePaths.Skip(1)
                        .Select(p => new { relative_path = p, format = "xml" })
                        .ToList()
                };
            }

            return CommonParser.SanitizeJson(serializer.Serialize(obj));
        }

        internal static string BuildConcatenatedRawXml(IReadOnlyList<byte[]> orderedXmlBytes)
        {
            if (orderedXmlBytes == null || orderedXmlBytes.Count == 0)
            {
                return null;
            }

            var sb = new StringBuilder();
            foreach (var chunk in orderedXmlBytes)
            {
                if (chunk == null || chunk.Length == 0)
                {
                    continue;
                }

                if (sb.Length > 0)
                {
                    sb.Append("\n<!-- ===== next xml ===== -->\n");
                }

                sb.Append(Encoding.UTF8.GetString(chunk));
            }

            return sb.Length == 0 ? null : sb.ToString();
        }
    }
}
