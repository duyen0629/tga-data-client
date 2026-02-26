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
                // Check tables directly (e.g. table with "Prerequisite unit requirements" in first row, no preceding p)
                for (var j = 0; j < children.Count; j++)
                {
                    if (children[j].Name != ns + "table")
                    {
                        continue;
                    }
                    if (QualificationPrerequisiteRequirementParser.IsPrerequisiteRequirementsTable(children[j], ns))
                    {
                        QualificationPrerequisiteRequirementParser.Parse(children[j], ns, packagingRules);
                        return;
                    }
                }
                // Also find table that follows a p with "prerequisite" and "requirement"
                for (var i = 0; i < children.Count; i++)
                {
                    if (children[i].Name != ns + "p")
                    {
                        continue;
                    }
                    var pText = CommonParser.ExtractInlineText(children[i]).Trim();
                    if (string.IsNullOrWhiteSpace(pText))
                    {
                        continue;
                    }
                    if (pText.IndexOf("prerequisite", StringComparison.OrdinalIgnoreCase) < 0
                        || pText.IndexOf("requirement", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }
                    for (var j = i + 1; j < children.Count; j++)
                    {
                        if (children[j].Name != ns + "table")
                        {
                            continue;
                        }
                        if (!QualificationPrerequisiteRequirementParser.IsPrerequisiteRequirementsTable(children[j], ns))
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
            const string unitCodePattern = @"\b([A-Z]{2,10}\d{2,6}[A-Z]?)\b";
            var textNode = topic.Element(ns + "Text");
            if (textNode == null)
            {
                return;
            }

            // elective rules: single-pass collection (content before Core, Specialisations after Core, no duplication)
            var electiveRulesParagraphElements = QualificationElectiveRulesParser.CollectElectiveRulesParagraphs(textNode, ns);

            // number: total_units, core_units_required, elective_units_required 
            QualificationPackagingCountsParser.Parse(textNode.Descendants(ns + "p"), ns, packagingRules);

            // core and elective units
            var children = textNode.Elements().ToList();
            var (coreUnitsFromTables, electiveUnitsFromTables, specialistFromTable, generalFromTable, _, inlinePrerequisitesFromTables) = QualificationUnitTablesParser.ParseCoreAndElectiveUnitsFromTables(children, ns, unitCodePattern);
            var coreUnitsFromParagraphs = QualificationCoreUnitsParser.Parse(children, ns, unitCodePattern);

            // Prefer paragraph-based core units when present (avoids misclassifying elective tables as core)
            var coreUnits = coreUnitsFromParagraphs.Count > 0 ? coreUnitsFromParagraphs : coreUnitsFromTables;

            if (coreUnits.Count > 0)
            {
                packagingRules["core_units"] = coreUnits;
            }

            // Elective units: from tables first, else from "Elective units" paragraph section (Group A, Group B, etc.)
            var electiveUnits = electiveUnitsFromTables.Count > 0
                ? electiveUnitsFromTables
                : QualificationElectiveUnitsParser.ParseFromParagraphs(children, ns, unitCodePattern);

            // specialist and general elective units: from paragraphs first, else from combined table (Core + Group A/B)
            var (specialistElectiveUnits, generalElectiveUnits) = QualificationSpecialistElectiveUnitsParser.Parse(children, ns, unitCodePattern);
            var specialistToMerge = specialistElectiveUnits.Count > 0 ? specialistElectiveUnits : specialistFromTable;
            var generalToMerge = generalElectiveUnits.Count > 0
                ? generalElectiveUnits.Cast<object>().ToList()
                : generalFromTable;

            // Merge all elective sources into elective_units. Format: { title, items } when groups exist (no key), else flat array.
            var mergedElectiveUnits = MergeElectiveUnits(electiveUnits, specialistToMerge, generalToMerge);
            if (mergedElectiveUnits.Count > 0)
            {
                packagingRules["elective_units"] = mergedElectiveUnits;
            }

            if (electiveRulesParagraphElements.Count > 0)
            {
                var electiveRulesItems = QualificationElectiveRulesParser.ParseToItems(
                    electiveRulesParagraphElements,
                    ns,
                    "elective_rules");
                packagingRules["elective_rules"] = electiveRulesItems;
            }

            if (inlinePrerequisitesFromTables.Count > 0)
            {
                var existing = (packagingRules["prerequisite_requirements"] as List<Dictionary<string, object>>) ?? new List<Dictionary<string, object>>();
                var nextOrder = existing.Count + 1;
                foreach (var pr in inlinePrerequisitesFromTables)
                {
                    pr["item_id"] = $"prerequisite_requirement-{nextOrder++}";
                    existing.Add(pr);
                }
                packagingRules["prerequisite_requirements"] = existing;
            }
        }

        /// <summary>
        /// Merges elective, specialist, and general elective units into a single elective_units list.
        /// When groups exist: [{ category, title, items }] (e.g. category "Specialist Elective Units").
        /// When no groups: flat [{ code, title, item_id, asterisk }, ...].
        /// </summary>
        private static List<object> MergeElectiveUnits(List<object> electiveUnits, List<object> specialistUnits, List<object> generalUnits)
        {
            var groupsWithCategory = new List<(string category, string fullTitle, List<Dictionary<string, object>> items)>();
            var flatUnits = new List<Dictionary<string, object>>();

            void AddSource(List<object> source, string fallbackCategory)
            {
                foreach (var item in source ?? new List<object>())
                {
                    if (item is Dictionary<string, object> dict)
                    {
                        if (dict.TryGetValue("items", out var itemsObj) && itemsObj is System.Collections.IEnumerable itemsEnumerable)
                        {
                            var items = new List<Dictionary<string, object>>();
                            foreach (var i in itemsEnumerable)
                            {
                                if (i is Dictionary<string, object> d)
                                    items.Add(d);
                            }
                            if (items.Count > 0)
                            {
                                var category = dict.TryGetValue("category", out var cat) ? (cat as string) ?? fallbackCategory : fallbackCategory;
                                var key = dict.TryGetValue("key", out var k) ? (k as string) ?? string.Empty : string.Empty;
                                var title = dict.TryGetValue("title", out var t) ? (t as string) ?? string.Empty : string.Empty;
                                var fullTitle = BuildGroupFullTitle(key, title);
                                if (!string.IsNullOrEmpty(fullTitle))
                                {
                                    groupsWithCategory.Add((category, fullTitle, items));
                                }
                                else if (!string.IsNullOrEmpty(category))
                                {
                                    groupsWithCategory.Add((category, category, items));
                                }
                                else
                                {
                                    flatUnits.AddRange(items);
                                }
                            }
                        }
                        else if (dict.ContainsKey("code"))
                        {
                            flatUnits.Add(dict);
                        }
                    }
                }
            }

            AddSource(electiveUnits, "Elective units");
            AddSource(specialistUnits, "Specialist Elective Units");
            AddSource(generalUnits, "General Elective Units");

            if (groupsWithCategory.Count > 0)
            {
                var result = groupsWithCategory.Select(g => (object)new Dictionary<string, object>
                {
                    { "category", g.category },
                    { "title", g.fullTitle },
                    { "items", g.items }
                }).ToList();
                if (flatUnits.Count > 0)
                {
                    result.Add(new Dictionary<string, object>
                    {
                        { "category", "Elective Units" },
                        { "title", "Elective units" },
                        { "items", flatUnits }
                    });
                }
                return result;
            }
            if (flatUnits.Count > 0)
            {
                return flatUnits.Cast<object>().ToList();
            }
            return new List<object>();
        }

        private static string BuildGroupFullTitle(string key, string title)
        {
            if (string.IsNullOrEmpty(key) || string.Equals(key, "Elective", StringComparison.OrdinalIgnoreCase))
            {
                return string.IsNullOrEmpty(title) ? string.Empty : title;
            }
            // "GeneralElectives" is a section header, not "Group X" - use title as-is
            if (string.Equals(key, "GeneralElectives", StringComparison.OrdinalIgnoreCase))
            {
                return string.IsNullOrEmpty(title) ? "General Electives" : title;
            }
            // Title is already full group header (e.g. "Group A", "Group A: Copper Cabling", "Group A - Building")
            if (!string.IsNullOrEmpty(title) && title.TrimStart().StartsWith("Group ", StringComparison.OrdinalIgnoreCase))
            {
                return title;
            }
            var groupPart = key.Length > 5 ? "Group " + key.Substring(5) : key;
            return string.IsNullOrEmpty(title) ? groupPart : $"{groupPart} - {title}";
        }
    }
}
