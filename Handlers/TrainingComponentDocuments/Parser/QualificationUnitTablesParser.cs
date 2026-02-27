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
            bool foundCoreTable,
            List<Dictionary<string, object>> inlinePrerequisitesFromTables) ParseCoreAndElectiveUnitsFromTables(
            List<XElement> children,
            XNamespace ns,
            string unitCodePattern)
        {
            var electiveGroupsOrdered = new List<(string key, string title, List<Dictionary<string, object>> items)>();
            var electiveGroupsMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var coreUnits = new List<Dictionary<string, object>>();
            var specialistFromTable = new List<object>();
            var generalFromTable = new List<object>();
            var inlinePrerequisitesFromTables = new List<Dictionary<string, object>>();
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

                var (currentElectiveGroup, currentElectiveGroupTitle) = GetElectiveGroupFromPrecedingParagraphs(children, i, ns);

                // Table may contain Core, Elective, Specialist Elective, and General Elective sections (split by section header rows)
                var (tableCoreUnits, tableElectiveUnits, tableElectiveGroups, tableSpecialistGroups, tableGeneralGroups, tableInlinePrereqs) = ParseTableWithCoreAndElectiveSections(table, ns, unitCodePattern);

                if (tableCoreUnits.Count > 0)
                {
                    coreUnits.AddRange(tableCoreUnits);
                    foundCoreTable = true;
                }

                if (tableInlinePrereqs.Count > 0)
                {
                    foreach (var pr in tableInlinePrereqs)
                    {
                        pr["item_id"] = $"prerequisite_requirement-{inlinePrerequisitesFromTables.Count + 1}";
                        inlinePrerequisitesFromTables.Add(pr);
                    }
                }

                if (tableElectiveGroups.Count > 0)
                {
                    foreach (var g in tableElectiveGroups.Where(x => x.items.Count > 0))
                    {
                        electiveGroupsOrdered.Add((g.key, g.title, g.items));
                        electiveGroupsMap[g.key] = electiveGroupsOrdered.Count - 1;
                    }
                }

                if (tableSpecialistGroups.Count > 0)
                {
                    foreach (var g in tableSpecialistGroups.Where(x => x.items.Count > 0))
                    {
                        specialistFromTable.Add(new Dictionary<string, object>
                        {
                            { "key", g.key },
                            { "title", g.title },
                            { "category", g.title },
                            { "items", g.items }
                        });
                    }
                }
                if (tableGeneralGroups.Count > 0)
                {
                    foreach (var g in tableGeneralGroups.Where(x => x.items.Count > 0))
                    {
                        generalFromTable.Add(new Dictionary<string, object>
                        {
                            { "key", g.key },
                            { "title", g.title },
                            { "category", g.title },
                            { "items", g.items }
                        });
                    }
                }

                AddElectiveUnitsToGroup(tableElectiveUnits, "Elective", electiveGroupsOrdered, electiveGroupsMap);

                if (tableCoreUnits.Count == 0 && tableElectiveUnits.Count == 0 && tableElectiveGroups.Count == 0)
                {
                    var itemIdPrefix = currentElectiveGroup != null ? $"elective_unit_{currentElectiveGroup}"
                        : !foundCoreTable ? "core_unit" : "elective_unit_Elective";
                    var tableUnitEntries = QualificationUnitTableUnitExtractor.ParseUnitRowsFromTable(table, ns, unitCodePattern, itemIdPrefix);

                    if (currentElectiveGroup != null && tableUnitEntries.Count > 0)
                    {
                        AddElectiveGroupIfNew(currentElectiveGroup, currentElectiveGroupTitle ?? string.Empty, electiveGroupsOrdered, electiveGroupsMap);
                        electiveGroupsOrdered[electiveGroupsMap[currentElectiveGroup]].items.AddRange(tableUnitEntries);
                    }
                    else if (tableUnitEntries.Count > 0 && !foundCoreTable)
                    {
                        coreUnits.AddRange(tableUnitEntries);
                        foundCoreTable = true;
                    }
                    else if (tableUnitEntries.Count > 0 && foundCoreTable)
                    {
                        AddElectiveUnitsToGroup(tableUnitEntries, "Elective", electiveGroupsOrdered, electiveGroupsMap);
                    }
                }
            }

            var electiveResult = BuildElectiveResult(electiveGroupsOrdered);
            return (coreUnits, electiveResult, specialistFromTable, generalFromTable, foundCoreTable, inlinePrerequisitesFromTables);
        }

        /// <summary>
        /// Parses a table that contains Core, Elective, Specialist Elective, and General Elective sections (section headers as rows).
        /// Section headers: "Core units", "Elective units", "Group A - Specialist Electives", "Group B - General Electives", etc.
        /// When in elective section, "Group A - General electives" and "Group B - Plant operation field of work" create elective groups (not general).
        /// </summary>
        private static (List<Dictionary<string, object>> coreUnits,
            List<Dictionary<string, object>> electiveUnits,
            List<(string key, string title, List<Dictionary<string, object>> items)> electiveGroups,
            List<(string key, string title, List<Dictionary<string, object>> items)> specialistElectiveGroups,
            List<(string key, string title, List<Dictionary<string, object>> items)> generalElectiveGroups,
            List<Dictionary<string, object>> inlinePrerequisites) ParseTableWithCoreAndElectiveSections(
            XElement table,
            XNamespace ns,
            string unitCodePattern)
        {
            var coreUnits = new List<Dictionary<string, object>>();
            var electiveUnits = new List<Dictionary<string, object>>();
            var electiveGroups = new List<(string key, string title, List<Dictionary<string, object>> items)>();
            var electiveGroupsMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var specialistElectiveGroups = new List<(string key, string title, List<Dictionary<string, object>> items)>();
            var specialistGroupsMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var generalElectiveGroups = new List<(string key, string title, List<Dictionary<string, object>> items)>();
            var generalGroupsMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var rows = table.Elements(ns + "tr").ToList();
            string currentSection = null;
            string currentElectiveGroup = null;
            string currentSpecialistGroup = null;
            string currentGeneralGroup = null;
            var coreOrder = 1;
            var electiveOrder = 1;
            var electiveOrderByGroup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var specialistOrderByGroup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var generalOrderByGroup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var unitsWithAsteriskForPrereq = new List<(string code, string title, int asteriskCount)>();
            var inlinePrerequisites = new List<Dictionary<string, object>>();
            var electiveHasPrerequisitesColumn = false;
            var coreHasPrerequisitesColumn = false;

            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var tds = row.Elements(ns + "td").ToList();
                var rowText = string.Concat(tds.Select(td => CommonParser.ExtractInlineText(td))).Trim();

                if (string.IsNullOrWhiteSpace(rowText))
                {
                    continue;
                }

                var rowTrimmed = rowText.Trim();

                if (QualificationUnitTableSectionMatcher.IsCoreSectionHeader(rowTrimmed))
                {
                    currentSection = "core";
                    currentElectiveGroup = null;
                    currentSpecialistGroup = null;
                    continue;
                }

                if (QualificationUnitTableSectionMatcher.IsElectiveSectionHeader(rowTrimmed))
                {
                    currentSection = "elective";
                    currentElectiveGroup = null;
                    currentSpecialistGroup = null;
                    currentGeneralGroup = null;
                    if (QualificationUnitTableSectionMatcher.IsCustomElectiveGroupHeader(rowTrimmed))
                    {
                        currentElectiveGroup = QualificationUnitTableSectionMatcher.GetGroupKeyFromCustomElectiveHeader(rowTrimmed);
                        AddGroupIfNew(currentElectiveGroup, rowTrimmed, electiveGroups, electiveGroupsMap, electiveOrderByGroup);
                        continue;
                    }
                }
                if (string.Equals(rowTrimmed, "General Electives", StringComparison.OrdinalIgnoreCase))
                {
                    currentSection = "general";
                    currentElectiveGroup = null;
                    currentSpecialistGroup = null;
                    currentGeneralGroup = "GeneralElectives";
                    AddGroupIfNew(currentGeneralGroup, "General Electives", generalElectiveGroups, generalGroupsMap, generalOrderByGroup);
                    continue;
                }
                var groupMatch = QualificationUnitTableSectionMatcher.GroupWithDelimiterRegex.Match(rowText);
                var groupMatchSpace = QualificationUnitTableSectionMatcher.GroupWithSpaceRegex.Match(rowText);
                var groupNoTitleMatch = QualificationUnitTableSectionMatcher.GroupNoTitleRegex.Match(rowText);
                if (groupMatch.Success && rowText.IndexOf("Specialist", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    currentSection = "specialist";
                    currentElectiveGroup = null;
                    currentSpecialistGroup = "Group" + groupMatch.Groups[1].Value.Trim();
                    AddGroupIfNew(currentSpecialistGroup, groupMatch.Groups[2].Value.Trim(), specialistElectiveGroups, specialistGroupsMap, specialistOrderByGroup);
                    continue;
                }
                if (groupMatch.Success && (rowText.IndexOf("General elective units", StringComparison.OrdinalIgnoreCase) >= 0
                    || rowText.IndexOf("General Electives", StringComparison.Ordinal) >= 0))
                {
                    currentSection = "general";
                    currentElectiveGroup = null;
                    currentSpecialistGroup = null;
                    currentGeneralGroup = "Group" + groupMatch.Groups[1].Value.Trim();
                    AddGroupIfNew(currentGeneralGroup, groupMatch.Groups[2].Value.Trim(), generalElectiveGroups, generalGroupsMap, generalOrderByGroup);
                    continue;
                }
                if (groupNoTitleMatch.Success && currentSection == "elective")
                {
                    currentElectiveGroup = "Group" + groupNoTitleMatch.Groups[1].Value.Trim();
                    AddGroupIfNew(currentElectiveGroup, string.Empty, electiveGroups, electiveGroupsMap, electiveOrderByGroup);
                    continue;
                }
                if (groupMatch.Success && currentSection == "elective")
                {
                    currentElectiveGroup = "Group" + groupMatch.Groups[1].Value.Trim();
                    AddGroupIfNew(currentElectiveGroup, groupMatch.Groups[2].Value.Trim(), electiveGroups, electiveGroupsMap, electiveOrderByGroup);
                    continue;
                }
                if (groupMatchSpace.Success && currentSection == "elective")
                {
                    currentElectiveGroup = "Group" + groupMatchSpace.Groups[1].Value.Trim();
                    AddGroupIfNew(currentElectiveGroup, groupMatchSpace.Value.Trim(), electiveGroups, electiveGroupsMap, electiveOrderByGroup);
                    continue;
                }
                if (currentSection == "elective" && (string.Equals(rowTrimmed, "Other electives", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(rowTrimmed, "Other", StringComparison.OrdinalIgnoreCase)))
                {
                    currentElectiveGroup = "OtherElectives";
                    AddGroupIfNew(currentElectiveGroup, rowTrimmed, electiveGroups, electiveGroupsMap, electiveOrderByGroup);
                    continue;
                }
                if (groupMatch.Success && currentSection != null)
                {
                    var groupKey = "Group" + groupMatch.Groups[1].Value.Trim();
                    var groupTitle = groupMatch.Groups[2].Value.Trim();
                    if (currentSection == "general")
                    {
                        currentGeneralGroup = groupKey;
                        AddGroupIfNew(currentGeneralGroup, groupTitle, generalElectiveGroups, generalGroupsMap, generalOrderByGroup);
                        continue;
                    }
                    if (currentSection == "specialist")
                    {
                        currentSpecialistGroup = groupKey;
                        AddGroupIfNew(currentSpecialistGroup, groupTitle, specialistElectiveGroups, specialistGroupsMap, specialistOrderByGroup);
                        continue;
                    }
                }

                // Table header with Prerequisites column (Unit code | Unit title | Prerequisites) - for core or elective
                if (tds.Count >= 3 && (currentSection == "core" || currentSection == "elective"))
                {
                    var cellTextsForHeader = tds.Select(td => CommonParser.ExtractInlineText(td)).Select(t => (t ?? string.Empty).Trim()).ToList();
                    if (cellTextsForHeader.Any(c => c.IndexOf("Prerequisites", StringComparison.OrdinalIgnoreCase) >= 0)
                        && !Regex.IsMatch(rowText, unitCodePattern))
                    {
                        if (currentSection == "core")
                            coreHasPrerequisitesColumn = true;
                        else
                            electiveHasPrerequisitesColumn = true;
                        continue;
                    }
                }

                if (QualificationUnitTablePrerequisites.TryParseInlinePrerequisiteRow(tds, ns, unitsWithAsteriskForPrereq, inlinePrerequisites))
                    continue;

                var entry = currentSection != null ? QualificationUnitTableUnitExtractor.ExtractUnitFromRow(tds, ns, unitCodePattern) : null;
                if (entry != null && tds.Count >= 3 && ((coreHasPrerequisitesColumn && currentSection == "core") || (electiveHasPrerequisitesColumn && currentSection == "elective")))
                {
                    var prereqList = QualificationUnitTablePrerequisites.ParsePrerequisitesFromCell(tds[2], ns, unitCodePattern);
                    if (prereqList != null && prereqList.Count > 0)
                        entry["prerequisites"] = prereqList;
                }

                if (entry != null)
                {
                    var asterisk = entry.ContainsKey("asterisk") ? Convert.ToInt32(entry["asterisk"]) : 0;
                    unitsWithAsteriskForPrereq.Add(((string)entry["code"], (string)entry["title"], asterisk));
                    if (currentSection == "core")
                    {
                        entry["item_id"] = $"core_unit-{coreOrder++}";
                        coreUnits.Add(entry);
                    }
                    else if (currentSection == "elective")
                    {
                        if (currentElectiveGroup != null)
                        {
                            var order = electiveOrderByGroup[currentElectiveGroup]++;
                            entry["item_id"] = $"elective_unit_{currentElectiveGroup}-{order}";
                            electiveGroups[electiveGroupsMap[currentElectiveGroup]].items.Add(entry);
                        }
                        else
                        {
                            entry["item_id"] = $"elective_unit_Elective-{electiveOrder++}";
                            electiveUnits.Add(entry);
                        }
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

            return (coreUnits, electiveUnits, electiveGroups, specialistElectiveGroups, generalElectiveGroups, inlinePrerequisites);
        }

        private static (string groupKey, string groupTitle) GetElectiveGroupFromPrecedingParagraphs(List<XElement> children, int tableIndex, XNamespace ns)
        {
            for (var j = tableIndex - 1; j >= 0 && j >= tableIndex - 5; j--)
            {
                if (children[j].Name == ns + "table") break;
                if (children[j].Name != ns + "p") continue;

                var prevText = (CommonParser.ExtractInlineText(children[j]) ?? string.Empty).Trim();
                var groupMatch = QualificationUnitTableSectionMatcher.PrecedingGroupRegex.Match(prevText);
                if (!groupMatch.Success) continue;

                var groupTitle = groupMatch.Groups[2].Success ? groupMatch.Groups[2].Value.Trim() : null;
                if (groupTitle == null && prevText.Length > 25) continue;
                if (groupTitle == null && j < tableIndex - 1)
                {
                    var betweenText = (CommonParser.ExtractInlineText(children[tableIndex - 1]) ?? string.Empty).Trim();
                    if (betweenText.Length <= 25) continue;
                }
                return ("Group" + groupMatch.Groups[1].Value.Trim(), groupTitle ?? string.Empty);
            }
            return (null, null);
        }

        private static void AddElectiveUnitsToGroup(
            List<Dictionary<string, object>> units,
            string groupKey,
            List<(string key, string title, List<Dictionary<string, object>> items)> electiveGroupsOrdered,
            Dictionary<string, int> electiveGroupsMap)
        {
            if (units.Count == 0) return;
            if (!electiveGroupsMap.ContainsKey(groupKey))
            {
                electiveGroupsOrdered.Add((groupKey, string.Empty, new List<Dictionary<string, object>>()));
                electiveGroupsMap[groupKey] = electiveGroupsOrdered.Count - 1;
            }
            electiveGroupsOrdered[electiveGroupsMap[groupKey]].items.AddRange(units);
        }

        private static void AddElectiveGroupIfNew(
            string groupKey,
            string groupTitle,
            List<(string key, string title, List<Dictionary<string, object>> items)> electiveGroupsOrdered,
            Dictionary<string, int> electiveGroupsMap)
        {
            if (electiveGroupsMap.ContainsKey(groupKey)) return;
            electiveGroupsOrdered.Add((groupKey, groupTitle, new List<Dictionary<string, object>>()));
            electiveGroupsMap[groupKey] = electiveGroupsOrdered.Count - 1;
        }

        private static List<object> BuildElectiveResult(List<(string key, string title, List<Dictionary<string, object>> items)> electiveGroupsOrdered)
        {
            var result = new List<object>();
            var namedGroups = electiveGroupsOrdered.Where(g => g.key != "Elective").ToList();
            const string electiveCategory = "Elective units";
            if (namedGroups.Count > 0)
            {
                foreach (var g in electiveGroupsOrdered.Where(g => g.items.Count > 0))
                {
                    result.Add(new Dictionary<string, object>
                    {
                        { "key", g.key },
                        { "title", g.title },
                        { "category", electiveCategory },
                        { "items", g.items }
                    });
                }
            }
            else if (electiveGroupsOrdered.Count > 0)
            {
                result.AddRange(electiveGroupsOrdered.SelectMany(g => g.items).Cast<object>());
            }
            return result;
        }

        private static void AddGroupIfNew(
            string groupKey,
            string groupTitle,
            List<(string key, string title, List<Dictionary<string, object>> items)> groups,
            Dictionary<string, int> groupsMap,
            Dictionary<string, int> orderByGroup)
        {
            if (groupsMap.ContainsKey(groupKey)) return;
            groups.Add((groupKey, groupTitle, new List<Dictionary<string, object>>()));
            groupsMap[groupKey] = groups.Count - 1;
            orderByGroup[groupKey] = 1;
        }

    }
}
