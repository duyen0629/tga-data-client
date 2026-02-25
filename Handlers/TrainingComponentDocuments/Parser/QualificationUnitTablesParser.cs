using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace TgaGateway2.Handlers.TrainingComponentDocuments.Parser
{
    // Parses core and elective unit tables from qualification packaging rules.
    internal static class QualificationUnitTablesParser
    {
        internal static (List<Dictionary<string, object>> coreUnits,
            List<object> electiveUnits,
            List<object> specialistElectiveUnitsFromTable,
            List<object> generalElectiveUnitsFromTable,
            bool foundCoreTable) ParseCoreAndElectiveUnitsFromTables(
            List<XElement> children,
            XNamespace ns,
            string unitCodePattern)
        {
            var electiveGroupsOrdered = new List<(string key, string title, List<Dictionary<string, object>> items)>();
            var electiveGroupsMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var coreUnits = new List<Dictionary<string, object>>();
            var specialistFromTable = new List<object>();
            var generalFromTable = new List<object>();
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

                if (QualificationPrerequisiteRequirementParser.IsPrerequisiteRequirementsTable(table, ns))
                {
                    continue;
                }

                string currentElectiveGroup = null;
                string currentElectiveGroupTitle = null;
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
                    // Try "Group X: Title" first (any length)
                    var groupWithTitleMatch = Regex.Match(prevText, @"^Group\s+([A-Za-z0-9]+)\s*:\s*(.+)$", RegexOptions.IgnoreCase);
                    if (groupWithTitleMatch.Success)
                    {
                        currentElectiveGroup = "Group" + groupWithTitleMatch.Groups[1].Value.Trim();
                        currentElectiveGroupTitle = groupWithTitleMatch.Groups[2].Value.Trim();
                        break;
                    }
                    // Try "Group X" (short text only, to avoid false matches)
                    if (prevText.Length <= 25)
                    {
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
                            currentElectiveGroupTitle = string.Empty;
                            break;
                        }
                    }
                }

                // Table may contain Core, Elective, Specialist Elective, and General Elective sections (split by section header rows)
                var (tableCoreUnits, tableElectiveUnits, tableSpecialistGroups, tableGeneralGroups) = ParseTableWithCoreAndElectiveSections(table, ns, unitCodePattern);

                if (tableCoreUnits.Count > 0)
                {
                    coreUnits.AddRange(tableCoreUnits);
                    foundCoreTable = true;
                }

                if (tableSpecialistGroups.Count > 0)
                {
                    foreach (var g in tableSpecialistGroups.Where(x => x.items.Count > 0))
                    {
                        specialistFromTable.Add(new Dictionary<string, object> { { "key", g.key }, { "title", g.title }, { "items", g.items } });
                    }
                }
                if (tableGeneralGroups.Count > 0)
                {
                    foreach (var g in tableGeneralGroups.Where(x => x.items.Count > 0))
                    {
                        generalFromTable.Add(new Dictionary<string, object> { { "key", g.key }, { "title", g.title }, { "items", g.items } });
                    }
                }

                if (tableElectiveUnits.Count > 0)
                {
                    const string electiveKey = "Elective";
                    if (!electiveGroupsMap.ContainsKey(electiveKey))
                    {
                        electiveGroupsOrdered.Add((electiveKey, string.Empty, new List<Dictionary<string, object>>()));
                        electiveGroupsMap[electiveKey] = electiveGroupsOrdered.Count - 1;
                    }
                    electiveGroupsOrdered[electiveGroupsMap[electiveKey]].items.AddRange(tableElectiveUnits);
                }

                // If table has no internal sections, use legacy logic (single section per table)
                if (tableCoreUnits.Count == 0 && tableElectiveUnits.Count == 0)
                {
                    string itemIdPrefix;
                    if (currentElectiveGroup != null)
                    {
                        itemIdPrefix = $"elective_unit_{currentElectiveGroup}";
                    }
                    else if (!foundCoreTable)
                    {
                        itemIdPrefix = "core_unit";
                    }
                    else
                    {
                        itemIdPrefix = "elective_unit_Elective";
                    }

                    var tableUnitEntries = ParseUnitRowsFromTable(table, ns, unitCodePattern, itemIdPrefix);

                    if (currentElectiveGroup != null && tableUnitEntries.Count > 0)
                    {
                        if (!electiveGroupsMap.ContainsKey(currentElectiveGroup))
                        {
                            electiveGroupsOrdered.Add((currentElectiveGroup, currentElectiveGroupTitle ?? string.Empty, new List<Dictionary<string, object>>()));
                            electiveGroupsMap[currentElectiveGroup] = electiveGroupsOrdered.Count - 1;
                        }
                        electiveGroupsOrdered[electiveGroupsMap[currentElectiveGroup]].items.AddRange(tableUnitEntries);
                    }
                    else if (tableUnitEntries.Count > 0 && !foundCoreTable)
                    {
                        coreUnits.AddRange(tableUnitEntries);
                        foundCoreTable = true;
                    }
                    else if (tableUnitEntries.Count > 0 && foundCoreTable)
                    {
                        const string electiveKey = "Elective";
                        if (!electiveGroupsMap.ContainsKey(electiveKey))
                        {
                            electiveGroupsOrdered.Add((electiveKey, string.Empty, new List<Dictionary<string, object>>()));
                            electiveGroupsMap[electiveKey] = electiveGroupsOrdered.Count - 1;
                        }
                        electiveGroupsOrdered[electiveGroupsMap[electiveKey]].items.AddRange(tableUnitEntries);
                    }
                }
            }

            var electiveResult = new List<object>();
            var namedGroups = electiveGroupsOrdered.Where(g => g.key != "Elective").ToList();
            if (namedGroups.Count > 0)
            {
                foreach (var g in electiveGroupsOrdered)
                {
                    if (g.items.Count > 0)
                    {
                        electiveResult.Add(new Dictionary<string, object>
                        {
                            { "key", g.key },
                            { "title", g.title },
                            { "items", g.items }
                        });
                    }
                }
            }
            else if (electiveGroupsOrdered.Count > 0)
            {
                var allUnits = electiveGroupsOrdered.SelectMany(g => g.items).ToList();
                electiveResult.AddRange(allUnits.Cast<object>());
            }

            return (coreUnits, electiveResult, specialistFromTable, generalFromTable, foundCoreTable);
        }

        /// <summary>
        /// Parses a table that contains Core, Elective, Specialist Elective, and General Elective sections (section headers as rows).
        /// Section headers: "Core units", "Elective units", "Group A - Specialist Electives", "Group B - General Electives", etc.
        /// </summary>
        private static (List<Dictionary<string, object>> coreUnits,
            List<Dictionary<string, object>> electiveUnits,
            List<(string key, string title, List<Dictionary<string, object>> items)> specialistElectiveGroups,
            List<(string key, string title, List<Dictionary<string, object>> items)> generalElectiveGroups) ParseTableWithCoreAndElectiveSections(
            XElement table,
            XNamespace ns,
            string unitCodePattern)
        {
            var coreUnits = new List<Dictionary<string, object>>();
            var electiveUnits = new List<Dictionary<string, object>>();
            var specialistElectiveGroups = new List<(string key, string title, List<Dictionary<string, object>> items)>();
            var specialistGroupsMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var generalElectiveGroups = new List<(string key, string title, List<Dictionary<string, object>> items)>();
            var generalGroupsMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var rows = table.Elements(ns + "tr").ToList();
            string currentSection = null;
            string currentSpecialistGroup = null;
            string currentGeneralGroup = null;
            var coreOrder = 1;
            var electiveOrder = 1;
            var specialistOrderByGroup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var generalOrderByGroup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var tds = row.Elements(ns + "td").ToList();
                var rowText = string.Concat(tds.Select(td => CommonParser.ExtractInlineText(td))).Trim();

                if (string.IsNullOrWhiteSpace(rowText))
                {
                    continue;
                }

                // Check for section header
                if (string.Equals(rowText, "Core units", StringComparison.OrdinalIgnoreCase) || string.Equals(rowText, "Core Units", StringComparison.OrdinalIgnoreCase))
                {
                    currentSection = "core";
                    currentSpecialistGroup = null;
                    continue;
                }
                if (string.Equals(rowText, "Elective units", StringComparison.OrdinalIgnoreCase) || string.Equals(rowText, "Elective Units", StringComparison.OrdinalIgnoreCase))
                {
                    currentSection = "elective";
                    currentSpecialistGroup = null;
                    continue;
                }
                // "Group A - Specialist Electives" or "Group A: Specialist Electives"
                var specialistMatch = Regex.Match(rowText, @"^Group\s+([A-Za-z0-9]+)\s*[-:]\s*(.+)$", RegexOptions.IgnoreCase);
                if (specialistMatch.Success && rowText.IndexOf("Specialist", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    currentSection = "specialist";
                    currentSpecialistGroup = "Group" + specialistMatch.Groups[1].Value.Trim();
                    var groupTitle = specialistMatch.Groups[2].Value.Trim();
                    if (!specialistGroupsMap.ContainsKey(currentSpecialistGroup))
                    {
                        specialistElectiveGroups.Add((currentSpecialistGroup, groupTitle, new List<Dictionary<string, object>>()));
                        specialistGroupsMap[currentSpecialistGroup] = specialistElectiveGroups.Count - 1;
                        specialistOrderByGroup[currentSpecialistGroup] = 1;
                    }
                    continue;
                }
                // "Group B - General Electives" or "Group B: General Electives"
                var generalMatch = Regex.Match(rowText, @"^Group\s+([A-Za-z0-9]+)\s*[-:]\s*(.+)$", RegexOptions.IgnoreCase);
                if (generalMatch.Success && rowText.IndexOf("General", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    currentSection = "general";
                    currentSpecialistGroup = null;
                    currentGeneralGroup = "Group" + generalMatch.Groups[1].Value.Trim();
                    var groupTitle = generalMatch.Groups[2].Value.Trim();
                    if (!generalGroupsMap.ContainsKey(currentGeneralGroup))
                    {
                        generalElectiveGroups.Add((currentGeneralGroup, groupTitle, new List<Dictionary<string, object>>()));
                        generalGroupsMap[currentGeneralGroup] = generalElectiveGroups.Count - 1;
                        generalOrderByGroup[currentGeneralGroup] = 1;
                    }
                    continue;
                }

                // Parse as unit row
                Dictionary<string, object> entry = null;
                if (tds.Count < 2)
                {
                    var singleText = tds.Count == 1 ? CommonParser.ExtractInlineText(tds[0]).Trim() : null;
                    var codeMatch = singleText != null ? Regex.Match(singleText, unitCodePattern) : Match.Empty;
                    if (codeMatch.Success && currentSection != null)
                    {
                        var code = codeMatch.Groups[1].Value;
                        var rawTitle = singleText.Substring(codeMatch.Index + codeMatch.Length).Trim().TrimStart('-', ':', ' ');
                        var (title, asterisk) = NormalizeTitleAndAsterisk(rawTitle ?? string.Empty);
                        entry = new Dictionary<string, object> { { "code", code }, { "title", title }, { "asterisk", asterisk } };
                    }
                }
                else
                {
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
                    if (unitCode != null && currentSection != null)
                    {
                        var (title, asterisk) = NormalizeTitleAndAsterisk(unitTitle ?? string.Empty);
                        if (string.IsNullOrWhiteSpace(title) && cellTexts.Count > 1)
                        {
                            title = (cellTexts[1] ?? string.Empty).Trim();
                        }
                        entry = new Dictionary<string, object> { { "code", unitCode }, { "title", title }, { "asterisk", asterisk } };
                    }
                }

                if (entry != null)
                {
                    if (currentSection == "core")
                    {
                        entry["item_id"] = $"core_unit-{coreOrder++}";
                        coreUnits.Add(entry);
                    }
                    else if (currentSection == "elective")
                    {
                        entry["item_id"] = $"elective_unit_Elective-{electiveOrder++}";
                        electiveUnits.Add(entry);
                    }
                    else if (currentSection == "specialist" && currentSpecialistGroup != null)
                    {
                        var order = specialistOrderByGroup[currentSpecialistGroup]++;
                        entry["item_id"] = $"specialist_elective_unit_{currentSpecialistGroup}-{order}";
                        specialistElectiveGroups[specialistGroupsMap[currentSpecialistGroup]].items.Add(entry);
                    }
                    else if (currentSection == "general" && currentGeneralGroup != null)
                    {
                        var order = generalOrderByGroup[currentGeneralGroup]++;
                        entry["item_id"] = $"general_elective_unit_{currentGeneralGroup}-{order}";
                        generalElectiveGroups[generalGroupsMap[currentGeneralGroup]].items.Add(entry);
                    }
                }
            }

            return (coreUnits, electiveUnits, specialistElectiveGroups, generalElectiveGroups);
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

        private static List<Dictionary<string, object>> ParseUnitRowsFromTable(
            XElement table,
            XNamespace ns,
            string unitCodePattern,
            string itemIdPrefix = null)
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

            var order = 1;
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
                        var entry = new Dictionary<string, object> { { "code", code }, { "title", title }, { "asterisk", asterisk } };
                        if (!string.IsNullOrEmpty(itemIdPrefix))
                        {
                            entry["item_id"] = $"{itemIdPrefix}-{order++}";
                        }
                        result.Add(entry);
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
                    var entry = new Dictionary<string, object> { { "code", unitCode }, { "title", title }, { "asterisk", asterisk } };
                    if (!string.IsNullOrEmpty(itemIdPrefix))
                    {
                        entry["item_id"] = $"{itemIdPrefix}-{order++}";
                    }
                    result.Add(entry);
                }
            }

            return result;
        }
    }
}
