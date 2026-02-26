using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TgaGateway2.Handlers.TrainingComponentDocuments;
using TgaGateway2.Handlers.TrainingComponentDocuments.Parser;
using TgaGateway2.Services;

namespace TgaGateway2.Tests
{
    [TestClass]
    public class TrainingComponentDocumentHandlerTests
    {
        public static IEnumerable<object[]> FixturePairs()
        {
            var fixtureRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fixtures", "Unit");
            if (!Directory.Exists(fixtureRoot))
            {
                yield break;
            }

            foreach (var xmlPath in Directory.EnumerateFiles(fixtureRoot, "*_XML.xml", SearchOption.TopDirectoryOnly))
            {
                var code = Path.GetFileName(xmlPath)?.Replace("_XML.xml", string.Empty);
                if (string.IsNullOrWhiteSpace(code))
                {
                    continue;
                }

                var jsonPath = Path.Combine(fixtureRoot, $"{code}_JSON.json");
                if (!File.Exists(jsonPath))
                {
                    continue;
                }

                yield return new object[] { code, xmlPath, jsonPath };
            }
        }

        public static IEnumerable<object[]> FixturePairsQualification()
        {
            var fixtureRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fixtures", "Qualification");
            if (!Directory.Exists(fixtureRoot))
            {
                yield break;
            }

            foreach (var xmlPath in Directory.EnumerateFiles(fixtureRoot, "*_XML.xml", SearchOption.TopDirectoryOnly))
            {
                var code = Path.GetFileName(xmlPath)?.Replace("_XML.xml", string.Empty);
                if (string.IsNullOrWhiteSpace(code))
                {
                    continue;
                }

                var jsonPath = Path.Combine(fixtureRoot, $"{code}_JSON.json");
                if (!File.Exists(jsonPath))
                {
                    continue;
                }

                yield return new object[] { code, xmlPath, jsonPath };
            }
        }

        [DataTestMethod]
        [DynamicData(nameof(FixturePairs), DynamicDataSourceType.Method)]
        public void BuildContentJsonForXml_MatchesExpectedFixtures(string code, string xmlPath, string jsonPath)
        {
            var xmlBytes = File.ReadAllBytes(xmlPath);
            var expectedJson = File.ReadAllText(jsonPath);
            var expectedRelativePath = ExtractRelativePath(expectedJson);
            var releaseNumber = ExtractReleaseNumber(expectedRelativePath) ?? "1";

            var releaseFiles = new List<ReleaseFileRow>
            {
                new ReleaseFileRow
                {
                    training_component_code = code,
                    release_number = releaseNumber,
                    relative_path = expectedRelativePath
                }
            };

            var xmlByPath = new Dictionary<string, byte[]>
            {
                { expectedRelativePath, xmlBytes }
            };

            var records = TrainingComponentDocumentTestHelper.BuildRecordsForReleaseFilesForUnitTest(
                code,
                releaseFiles,
                path => xmlByPath[path]);

            Assert.AreEqual(1, records.Count, $"{code}: Expected a single record.");
            var actualJson = records[0].ContentJson?.Value;

            AssertJsonEquivalent(expectedJson, actualJson, code);
        }

        [DataTestMethod]
        [DynamicData(nameof(FixturePairsQualification), DynamicDataSourceType.Method)]
        public void BuildContentJsonForXml_Qualification_MatchesExpectedFixtures(string code, string xmlPath, string jsonPath)
        {
            var xmlBytes = File.ReadAllBytes(xmlPath);
            var expectedJson = File.ReadAllText(jsonPath);
            var expectedRelativePath = ExtractRelativePath(expectedJson);
            var releaseNumber = ExtractReleaseNumber(expectedRelativePath) ?? "1";

            var releaseFiles = new List<ReleaseFileRow>
            {
                new ReleaseFileRow
                {
                    training_component_code = code,
                    release_number = releaseNumber,
                    relative_path = expectedRelativePath
                }
            };

            var xmlByPath = new Dictionary<string, byte[]>
            {
                { expectedRelativePath, xmlBytes }
            };

            var records = TrainingComponentDocumentTestHelper.BuildRecordsForReleaseFilesForQualificationTest(
                code,
                releaseFiles,
                path => xmlByPath[path]);

            Assert.AreEqual(1, records.Count, $"{code}: Expected a single qualification record.");
            var actualJson = records[0].ContentJson?.Value;

            AssertJsonEquivalent(expectedJson, actualJson, code);
        }

        [TestMethod]
        public void FixturesArePresent()
        {
            var fixturesRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fixtures");
            Assert.IsTrue(Directory.Exists(fixturesRoot), $"Fixture folder missing: {fixturesRoot}");
            var unitFixtures = Directory.Exists(Path.Combine(fixturesRoot, "Unit"))
                ? Directory.EnumerateFiles(Path.Combine(fixturesRoot, "Unit"), "*_XML.xml").Any()
                : false;
            var qualificationFixtures = Directory.Exists(Path.Combine(fixturesRoot, "Qualification"))
                ? Directory.EnumerateFiles(Path.Combine(fixturesRoot, "Qualification"), "*_XML.xml").Any()
                : false;
            Assert.IsTrue(unitFixtures || qualificationFixtures, "No XML fixtures found in Unit or Qualification folders.");
        }

        private static string ExtractRelativePath(string json)
        {
            var serializer = new JavaScriptSerializer();
            var root = serializer.DeserializeObject(json) as Dictionary<string, object>;
            if (root == null || !root.TryGetValue("source", out var sourceObj))
            {
                return null;
            }

            var source = sourceObj as Dictionary<string, object>;
            if (source == null || !source.TryGetValue("complete", out var completeObj))
            {
                return null;
            }

            var complete = completeObj as Dictionary<string, object>;
            if (complete == null || !complete.TryGetValue("relative_path", out var relativePath))
            {
                return null;
            }

            return relativePath?.ToString();
        }

        private static string ExtractReleaseNumber(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return null;
            }

            var match = Regex.Match(relativePath, @"_R(?<release>\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups["release"].Value : null;
        }

        private static void AssertJsonEquivalent(string expectedJson, string actualJson, string code)
        {
            var serializer = new JavaScriptSerializer();
            var expected = serializer.DeserializeObject(expectedJson);
            var actual = serializer.DeserializeObject(actualJson);

            AssertObjectsEqual(expected, actual, $"[{code}] $");
        }

        private static void AssertObjectsEqual(object expected, object actual, string path)
        {
            if (expected == null && actual == null)
            {
                return;
            }

            if (expected == null || actual == null)
            {
                Assert.Fail($"{path}: One value is null while the other is not.");
            }

            if (expected is Dictionary<string, object> expectedDict && actual is Dictionary<string, object> actualDict)
            {
                Assert.IsTrue(actualDict.Count >= expectedDict.Count, $"{path}: Actual has fewer keys than expected.");
                foreach (var key in expectedDict.Keys)
                {
                    Assert.IsTrue(actualDict.ContainsKey(key), $"{path}: Missing key '{key}'.");
                    AssertObjectsEqual(expectedDict[key], actualDict[key], $"{path}.{key}");
                }
                return;
            }

            if (expected is object[] expectedArray && actual is object[] actualArray)
            {
                Assert.AreEqual(expectedArray.Length, actualArray.Length, $"{path}: Array length mismatch.");
                for (var i = 0; i < expectedArray.Length; i++)
                {
                    AssertObjectsEqual(expectedArray[i], actualArray[i], $"{path}[{i}]");
                }
                return;
            }

            if (expected is ArrayList expectedList && actual is ArrayList actualList)
            {
                Assert.AreEqual(expectedList.Count, actualList.Count, $"{path}: List count mismatch.");
                for (var i = 0; i < expectedList.Count; i++)
                {
                    AssertObjectsEqual(expectedList[i], actualList[i], $"{path}[{i}]");
                }
                return;
            }

            if (expected is string expectedString && actual is string actualString)
            {
                var normalizedExpected = NormalizeString(expectedString);
                var normalizedActual = NormalizeString(actualString);
                Assert.AreEqual(normalizedExpected, normalizedActual, $"{path}: Value mismatch.");
                return;
            }

            Assert.AreEqual(expected, actual, $"{path}: Value mismatch.");
        }

        private static string NormalizeString(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return Regex.Replace(value, @"\s+", string.Empty);
        }

        private static string BuildContentJsonForXmlForUnit(byte[] xmlBytes, string relativePath)
        {
            var sections = UnitParser.ParserSectionFromXmlForUnit(xmlBytes);
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
            return CommonParser.SanitizeJson(serializer.Serialize(contentJson));
        }
    }
}
