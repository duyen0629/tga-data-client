using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using TgaGateway2.Handlers.TrainingComponentDocuments.Helper;
using TgaGateway2.Handlers.TrainingComponentDocuments.Type;

namespace TgaGateway2.Handlers.TrainingComponentDocuments.Parser
{
    internal static class QualificationParser
    {
        internal static (List<DocumentSection> sections, Dictionary<string, object> packagingRules) ParserSectionFromXmlForQualification(byte[] xmlBytes)
        {
            var packagingRules = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            packagingRules["prerequisite_requirements"] = new List<Dictionary<string, object>>();

            if (xmlBytes == null || xmlBytes.Length == 0)
            {
                return (new List<DocumentSection>(), packagingRules);
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

                    if (SectionKeyHelper.SectionKeyEquals(key, "packaging_rules"))
                    {
                        var section = CommonParser.ParseTopicToSection(topic, ns, key, title.Trim(), order++);
                        sections.Add(section);
                        ParsePackagingRulesFromTopic(topic, ns, packagingRules);
                    }
                    else
                    {
                        sections.Add(CommonParser.ParseTopicToSection(topic, ns, key, title.Trim(), order++));
                    }
                }

                FindAndParsePrerequisiteRequirementsTable(doc, ns, packagingRules);

                return (sections, packagingRules);
            }
        }

        private static void FindAndParsePrerequisiteRequirementsTable(XDocument doc, XNamespace ns, Dictionary<string, object> packagingRules)
        {
            foreach (var textNode in doc.Descendants(ns + "Text"))
            {
                var children = textNode.Elements().ToList();
                for (var i = 0; i < children.Count; i++)
                {
                    if (children[i].Name != ns + "p")
                    {
                        continue;
                    }
                    var pText = CommonParser.ExtractInlineText(children[i]).Trim();
                    if (string.IsNullOrWhiteSpace(pText) || pText.IndexOf("Prerequisite requirements", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }
                    for (var j = i + 1; j < children.Count; j++)
                    {
                        if (children[j].Name != ns + "table")
                        {
                            continue;
                        }
                        if (!QualificationUnitTablesParser.IsPrerequisiteRequirementsTable(children[j], ns))
                        {
                            continue;
                        }
                        QualificationPrerequisiteRequirementParser.Parse(children[j], ns, packagingRules);
                        return;
                    }
                    break;
                }
            }
        }

        private static void ParsePackagingRulesFromTopic(XElement topic, XNamespace ns, Dictionary<string, object> packagingRules)
        {
            const string unitCodePattern = @"\b([A-Z]{2,10}\d{3,6}[A-Z]?)\b";
            var textNode = topic.Element(ns + "Text");
            if (textNode == null)
            {
                return;
            }

            // elective rules paragraphs
            var electiveRulesParagraphElements = QualificationElectiveRulesParser.CollectElectiveRulesParagraphs(textNode, ns);

            // number: total_units, core_units_required, elective_units_required 
            QualificationPackagingCountsParser.Parse(textNode.Descendants(ns + "p"), ns, packagingRules);

            // core and elective units
            var children = textNode.Elements().ToList();
            var (coreUnits, electiveGroups, _) = QualificationUnitTablesParser.ParseCoreAndElectiveUnitsFromTables(children, ns, unitCodePattern);

            // format: core units can be in table or paragraphs
            if (coreUnits.Count == 0)
            {
                coreUnits = QualificationCoreUnitsParser.Parse(children, ns, unitCodePattern);
            }

            if (coreUnits.Count > 0)
            {
                packagingRules["core_units"] = coreUnits;
            }

            if (electiveGroups.Count > 0)
            {
                packagingRules["elective_units"] = electiveGroups;
            }

            // specialist and general elective units
            var (specialistElectiveGroups, generalElectiveUnits) = QualificationSpecialistElectiveUnitsParser.Parse(children, ns, unitCodePattern);
            if (specialistElectiveGroups.Count > 0)
            {
                packagingRules["specialist_elective_units"] = specialistElectiveGroups;
            }
            if (generalElectiveUnits.Count > 0)
            {
                packagingRules["general_elective_units"] = generalElectiveUnits;
            }

            if (electiveRulesParagraphElements.Count > 0)
            {
                var electiveRulesItems = CommonParser.ParseParagraphElementsToItems(
                    electiveRulesParagraphElements,
                    "elective_rules");
                packagingRules["elective_rules"] = electiveRulesItems;
            }
        }

    }
}
