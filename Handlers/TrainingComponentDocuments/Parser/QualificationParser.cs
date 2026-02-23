using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
                        if (!IsPrerequisiteRequirementsTable(children[j], ns))
                        {
                            continue;
                        }
                        ParsePrerequisiteRequirementsTable(children[j], ns, packagingRules);
                        return;
                    }
                    break;
                }
            }
        }

        private static bool IsPrerequisiteRequirementsTable(XElement table, XNamespace ns)
        {
            var rows = table.Elements(ns + "tr").ToList();
            if (rows.Count == 0)
            {
                return false;
            }
            var firstRowCells = rows[0].Elements(ns + "td").Concat(rows[0].Elements(ns + "th")).ToList();
            if (firstRowCells.Count < 2)
            {
                return false;
            }
            var c0 = CommonParser.ExtractInlineText(firstRowCells[0]).Trim();
            var c1 = CommonParser.ExtractInlineText(firstRowCells[1]).Trim();
            return c0.IndexOf("Unit of competency", StringComparison.OrdinalIgnoreCase) >= 0
                   && c1.IndexOf("Prerequisite requirement", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void ParsePackagingRulesFromTopic(XElement topic, XNamespace ns, Dictionary<string, object> packagingRules)
        {
            var textNode = topic.Element(ns + "Text");
            if (textNode == null)
            {
                return;
            }

            foreach (var p in textNode.Descendants(ns + "p"))
            {
                var text = CommonParser.ExtractInlineText(p) ?? string.Empty;

                var totalMatch = Regex.Match(text, @"(\d+)\s*units?\s*of\s*competency", RegexOptions.IgnoreCase);
                if (totalMatch.Success && int.TryParse(totalMatch.Groups[1].Value, out var totalUnits))
                {
                    packagingRules["total_units"] = totalUnits;
                }

                var coreMatch = Regex.Match(text, @"(\d+)\s*core\s*units?", RegexOptions.IgnoreCase);
                if (coreMatch.Success && int.TryParse(coreMatch.Groups[1].Value, out var coreRequired))
                {
                    packagingRules["core_units_required"] = coreRequired;
                }

                var electiveMatch = Regex.Match(text, @"(\d+)\s*elective\s*units?", RegexOptions.IgnoreCase);
                if (electiveMatch.Success && int.TryParse(electiveMatch.Groups[1].Value, out var electiveRequired))
                {
                    packagingRules["elective_units_required"] = electiveRequired;
                }
            }

            const string unitCodePattern = @"\b([A-Z]{2,10}\d{3,6}[A-Z]?)\b";
            var children = textNode.Elements().ToList();
            var electiveGroups = new Dictionary<string, List<Dictionary<string, object>>>(StringComparer.OrdinalIgnoreCase);
            var coreUnits = new List<Dictionary<string, object>>();
            var foundCoreTable = false;

            for (var i = 0; i < children.Count; i++)
            {
                var node = children[i];
                if (node.Name != ns + "table")
                {
                    continue;
                }

                var table = node;
                var rows = table.Elements(ns + "tr").ToList();
                if (rows.Count == 0)
                {
                    continue;
                }

                string currentElectiveGroup = null;
                for (var j = i - 1; j >= 0 && j >= i - 5; j--)
                {
                    if (children[j].Name == ns + "table")
                    {
                        break;
                    }
                    if (children[j].Name != ns + "p")
                    {
                        continue;
                    }
                    var prevText = (CommonParser.ExtractInlineText(children[j]) ?? string.Empty).Trim();
                    if (prevText.Length > 25)
                    {
                        continue;
                    }
                    var groupMatch = Regex.Match(prevText, @"^Group\s+([A-Za-z0-9]+)\s*$", RegexOptions.IgnoreCase);
                    if (groupMatch.Success)
                    {
                        if (j < i - 1)
                        {
                            var betweenText = (CommonParser.ExtractInlineText(children[i - 1]) ?? string.Empty).Trim();
                            if (betweenText.Length <= 25)
                            {
                                continue;
                            }
                        }
                        currentElectiveGroup = "Group" + groupMatch.Groups[1].Value.Trim();
                        break;
                    }
                }

                var tableUnitEntries = ParseUnitRowsFromTable(table, ns, unitCodePattern);

                if (currentElectiveGroup != null && tableUnitEntries.Count > 0)
                {
                    if (!electiveGroups.ContainsKey(currentElectiveGroup))
                    {
                        electiveGroups[currentElectiveGroup] = new List<Dictionary<string, object>>();
                    }
                    electiveGroups[currentElectiveGroup].AddRange(tableUnitEntries);
                }
                else if (tableUnitEntries.Count > 0 && !foundCoreTable)
                {
                    coreUnits.AddRange(tableUnitEntries);
                    foundCoreTable = true;
                }
                else if (tableUnitEntries.Count > 0 && foundCoreTable)
                {
                    if (!electiveGroups.ContainsKey("Elective"))
                    {
                        electiveGroups["Elective"] = new List<Dictionary<string, object>>();
                    }
                    electiveGroups["Elective"].AddRange(tableUnitEntries);
                }
            }

            if (coreUnits.Count > 0)
            {
                packagingRules["core_units"] = coreUnits;
            }

            if (electiveGroups.Count > 0)
            {
                packagingRules["elective_units"] = electiveGroups;
            }
        }

        private static void ParsePrerequisiteRequirementsTable(XElement table, XNamespace ns, Dictionary<string, object> packagingRules)
        {
            const string unitCodePattern = @"\b([A-Z]{2,10}\d{3,6}[A-Z]?)\b";
            var rows = table.Elements(ns + "tr").ToList();
            var headerRowIndex = -1;
            for (var i = 0; i < rows.Count; i++)
            {
                var headerAttr = rows[i].Attribute("header")?.Value;
                if (string.Equals(headerAttr, "true", StringComparison.OrdinalIgnoreCase))
                {
                    headerRowIndex = i;
                    break;
                }
            }
            if (headerRowIndex < 0)
            {
                headerRowIndex = 0;
            }

            var prerequisiteList = new List<Dictionary<string, object>>();
            for (var i = 0; i < rows.Count; i++)
            {
                if (i == headerRowIndex)
                {
                    continue;
                }

                var tds = rows[i].Elements(ns + "td").ToList();
                if (tds.Count < 2)
                {
                    continue;
                }

                var cell0Text = CommonParser.ExtractInlineText(tds[0]).Trim();
                var cell1Text = CommonParser.ExtractInlineText(tds[1]).Trim();
                var unitOfCompetency = ParseCodeAndTitleFromCell(cell0Text, unitCodePattern)
                    ?? new Dictionary<string, object> { { "code", string.Empty }, { "title", cell0Text }, { "asterisk", false } };
                var prerequisiteRequirement = ParseCodeAndTitleFromCell(cell1Text, unitCodePattern);

                bool unitTitleEmptyOrSymbol = string.IsNullOrWhiteSpace(unitOfCompetency["title"]?.ToString())
                    || unitOfCompetency["title"]?.ToString().Trim() == "*";
                if (unitTitleEmptyOrSymbol && !string.IsNullOrWhiteSpace(cell1Text) && prerequisiteRequirement == null)
                {
                    unitOfCompetency["title"] = cell1Text;
                    prerequisiteRequirement = new Dictionary<string, object> { { "code", string.Empty }, { "title", string.Empty }, { "asterisk", false } };
                }

                if (prerequisiteRequirement == null)
                {
                    prerequisiteRequirement = new Dictionary<string, object> { { "code", string.Empty }, { "title", cell1Text }, { "asterisk", false } };
                }

                prerequisiteList.Add(new Dictionary<string, object>
                {
                    { "unit_of_competency", unitOfCompetency },
                    { "prerequisite_requirement", prerequisiteRequirement }
                });
            }

            packagingRules["prerequisite_requirements"] = prerequisiteList;
        }

        internal static bool IsPrerequisiteRequirementsSection(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;
            return SectionKeyHelper.SectionKeyEquals(key, "prerequisite_requirements") ||
                   SectionKeyHelper.SectionKeyEquals(key, "prerequisites") ||
                   key.IndexOf("prerequisite", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Dictionary<string, object> ParseCodeAndTitleFromCell(string cellText, string unitCodePattern)
        {
            if (string.IsNullOrWhiteSpace(cellText))
            {
                return null;
            }

            var match = Regex.Match(cellText, unitCodePattern);
            if (!match.Success)
            {
                return null;
            }

            var code = match.Groups[1].Value;
            var title = cellText.Substring(match.Index + match.Length).Trim().TrimStart('-', ':', ' ');
            var asterisk = false;
            if (!string.IsNullOrEmpty(title) && (title.StartsWith("*", StringComparison.Ordinal) || title.StartsWith(" *", StringComparison.Ordinal)))
            {
                asterisk = true;
                title = title.TrimStart(' ', '*').Trim();
            }
            return new Dictionary<string, object>
            {
                { "code", code },
                { "title", title ?? string.Empty },
                { "asterisk", asterisk }
            };
        }

        private static (string title, bool asterisk) NormalizeTitleAndAsterisk(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return (string.Empty, false);
            }
            var t = title.Trim().TrimStart('-', ':', ' ');
            var asterisk = t.StartsWith("*", StringComparison.Ordinal) || t.StartsWith(" *", StringComparison.Ordinal);
            if (asterisk)
            {
                t = t.TrimStart(' ', '*').Trim();
            }
            return (t ?? string.Empty, asterisk);
        }

        private static List<Dictionary<string, object>> ParseUnitRowsFromTable(XElement table, XNamespace ns, string unitCodePattern)
        {
            var result = new List<Dictionary<string, object>>();
            var rows = table.Elements(ns + "tr").ToList();
            var headerRowIndex = -1;
            for (var i = 0; i < rows.Count; i++)
            {
                var headerAttr = rows[i].Attribute("header")?.Value;
                if (string.Equals(headerAttr, "true", StringComparison.OrdinalIgnoreCase))
                {
                    headerRowIndex = i;
                    break;
                }
            }

            for (var i = 0; i < rows.Count; i++)
            {
                if (i == headerRowIndex)
                {
                    continue;
                }

                var tds = rows[i].Elements(ns + "td").ToList();
                if (tds.Count < 2)
                {
                    var singleText = tds.Count == 1 ? CommonParser.ExtractInlineText(tds[0]).Trim() : null;
                    var codeMatch = singleText != null ? Regex.Match(singleText, unitCodePattern) : Match.Empty;
                    if (codeMatch.Success)
                    {
                        var code = codeMatch.Groups[1].Value;
                        var rawTitle = singleText.Substring(codeMatch.Index + codeMatch.Length).Trim().TrimStart('-', ':', ' ');
                        var (title, asterisk) = NormalizeTitleAndAsterisk(rawTitle ?? string.Empty);
                        result.Add(new Dictionary<string, object> { { "code", code }, { "title", title }, { "asterisk", asterisk } });
                    }
                    continue;
                }

                var cellTexts = tds.Select(td => CommonParser.ExtractInlineText(td)).Select(t => (t ?? string.Empty).Trim()).ToList();
                string unitCode = null;
                string unitTitle = null;

                for (var c = 0; c < cellTexts.Count; c++)
                {
                    var cell = cellTexts[c];
                    var match = Regex.Match(cell, unitCodePattern);
                    if (match.Success)
                    {
                        unitCode = match.Groups[1].Value;
                        unitTitle = cell.Length > match.Index + match.Length
                            ? cell.Substring(match.Index + match.Length).Trim().TrimStart('-', ':', ' ')
                            : (c + 1 < cellTexts.Count ? cellTexts[c + 1] : null) ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(unitTitle) && c + 1 < cellTexts.Count)
                        {
                            unitTitle = cellTexts[c + 1];
                        }
                        break;
                    }
                }

                if (unitCode != null)
                {
                    var (title, asterisk) = NormalizeTitleAndAsterisk(unitTitle ?? string.Empty);
                    if (string.IsNullOrWhiteSpace(title) && cellTexts.Count > 1)
                    {
                        title = (cellTexts[1] ?? string.Empty).Trim();
                    }
                    result.Add(new Dictionary<string, object> { { "code", unitCode }, { "title", title }, { "asterisk", asterisk } });
                }
            }

            return result;
        }
    }
}
