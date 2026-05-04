using System;
using System.Collections.Generic;
using TgaGateway2.Handlers.TrainingComponentDocuments;
using TgaGateway2.Handlers.TrainingComponentDocuments.Helper;
using TgaGateway2.Models;
using TgaGateway2.Services;

namespace TgaGateway2.Tests
{
    /// <summary>
    /// Test helper: builds document records from release files using a provided XML bytes provider (e.g. in-memory fixtures).
    /// </summary>
    internal static class TrainingComponentDocumentTestHelper
    {
        internal static List<TrainingComponentDocumentRecord> BuildRecordsForReleaseFilesForUnitTest(
            string trainingComponentCode,
            List<ReleaseFileRow> releaseFiles,
            Func<string, byte[]> xmlBytesProvider)
        {
            if (xmlBytesProvider == null)
            {
                throw new ArgumentNullException(nameof(xmlBytesProvider));
            }

            var candidates = ReleaseFileHelper.SelectReleaseFilesByRelease(releaseFiles);
            var records = new List<TrainingComponentDocumentRecord>();

            foreach (var candidate in candidates)
            {
                if (candidate?.XmlSources == null || candidate.XmlSources.Count == 0)
                {
                    continue;
                }

                var ordered = new List<(byte[] bytes, string relativePath)>();
                foreach (var src in candidate.XmlSources)
                {
                    var p = src?.XmlPath;
                    if (string.IsNullOrWhiteSpace(p))
                    {
                        continue;
                    }

                    var xmlBytes = xmlBytesProvider(p);
                    if (xmlBytes == null || xmlBytes.Length == 0)
                    {
                        throw new Exception($"Missing XML bytes for {p}");
                    }

                    ordered.Add((xmlBytes, p));
                }

                if (ordered.Count == 0)
                {
                    continue;
                }

                var record = TrainingComponentDocumentHandler.BuildRecordFromXmlBytesForUnit(
                    trainingComponentCode,
                    candidate.ReleaseNumber,
                    componentType: null,
                    usageRecommendation: null,
                    ordered);

                records.Add(record);
            }

            return records;
        }

        internal static List<TrainingComponentDocumentRecord> BuildRecordsForReleaseFilesForQualificationTest(
            string trainingComponentCode,
            List<ReleaseFileRow> releaseFiles,
            Func<string, byte[]> xmlBytesProvider,
            string componentType = null)
        {
            if (xmlBytesProvider == null)
            {
                throw new ArgumentNullException(nameof(xmlBytesProvider));
            }

            var candidates = ReleaseFileHelper.SelectReleaseFilesByRelease(releaseFiles);
            var records = new List<TrainingComponentDocumentRecord>();

            foreach (var candidate in candidates)
            {
                if (candidate?.XmlSources == null || candidate.XmlSources.Count == 0)
                {
                    continue;
                }

                var ordered = new List<(byte[] bytes, string relativePath)>();
                foreach (var src in candidate.XmlSources)
                {
                    var p = src?.XmlPath;
                    if (string.IsNullOrWhiteSpace(p))
                    {
                        continue;
                    }

                    var xmlBytes = xmlBytesProvider(p);
                    if (xmlBytes == null || xmlBytes.Length == 0)
                    {
                        throw new Exception($"Missing XML bytes for {p}");
                    }

                    ordered.Add((xmlBytes, p));
                }

                if (ordered.Count == 0)
                {
                    continue;
                }

                var record = TrainingComponentDocumentHandler.BuildRecordFromXmlBytesForQualification(
                    trainingComponentCode,
                    candidate.ReleaseNumber,
                    componentType,
                    usageRecommendation: null,
                    ordered);

                records.Add(record);
            }

            return records;
        }
    }
}
