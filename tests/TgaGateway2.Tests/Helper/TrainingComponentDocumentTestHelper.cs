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

                var record = TrainingComponentDocumentHandler.BuildRecordFromXmlBytesForUnit(
                    trainingComponentCode,
                    candidate.ReleaseNumber,
                    componentType: null,
                    usageRecommendation: null,
                    xmlPath,
                    "xml",
                    xmlBytes);

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

                var record = TrainingComponentDocumentHandler.BuildRecordFromXmlBytesForQualification(
                    trainingComponentCode,
                    candidate.ReleaseNumber,
                    componentType,
                    usageRecommendation: null,
                    xmlPath,
                    "xml",
                    xmlBytes);

                records.Add(record);
            }

            return records;
        }
    }
}
