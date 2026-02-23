using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using TgaGateway2.Handlers.TrainingComponentDocuments.Helper;
using TgaGateway2.Handlers.TrainingComponentDocuments.Type;

namespace TgaGateway2.Handlers.TrainingComponentDocuments.Parser
{
    /// <summary>
    /// Parses unit XML into document sections (no packaging rules).
    /// </summary>
    internal static class UnitParser
    {
        internal static List<DocumentSection> ParserSectionFromXmlForUnit(byte[] xmlBytes)
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

                    var key = SectionKeyHelper.NormalizeKey(title);
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        continue;
                    }

                    if (seenKeys.Contains(key))
                    {
                        continue;
                    }

                    seenKeys.Add(key);
                    sections.Add(CommonParser.ParseTopicToSection(topic, ns, key, title.Trim(), order++));
                }

                return sections;
            }
        }
    }
}
