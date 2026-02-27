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
                    // Match "Group X - Title", "Group X: Title", or "Group X" (short only)
                    var groupMatch = Regex.Match(prevText, @"^Group\s+([A-Za-z0-9]+)(?:\s*[-:\u2013\u2014]\s*(.+))?\s*$", RegexOptions.IgnoreCase);
                    if (!groupMatch.Success) continue;
                    var groupTitle = groupMatch.Groups[2].Success ? groupMatch.Groups[2].Value.Trim() : null;
                    if (groupTitle == null && prevText.Length > 25) continue; // "Group X" without title only when short
                    if (groupTitle == null && j < i - 1)
                    {
                        var betweenText = (CommonParser.ExtractInlineText(children[i - 1]) ?? string.Empty).Trim();
                        if (betweenText.Length <= 25) continue;
                    }
                    currentElectiveGroup = "Group" + groupMatch.Groups[1].Value.Trim();
                    currentElectiveGroupTitle = groupTitle ?? string.Empty;
                    break;
                }

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

                if (tableElectiveUnits.Count > 0 && tableElectiveGroups.Count == 0)
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
                if (tableCoreUnits.Count == 0 && tableElectiveUnits.Count == 0 && tableElectiveGroups.Count == 0)
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
            const string electiveCategory = "Elective units";
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
                            { "category", electiveCategory },
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
            const string prereqLinePattern = @"^(\*+)\s*Prerequisite\s+unit\s+([A-Z]{2,10}\d{2,6}[A-Z]?)\s+(.+)$";
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

                // Check for section header
                var rowTrimmed = rowText.Trim();
                if (string.Equals(rowText, "Core units", StringComparison.OrdinalIgnoreCase) || string.Equals(rowText, "Core Units", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(rowTrimmed, "Core", StringComparison.OrdinalIgnoreCase)
                    || rowTrimmed.StartsWith("Core units", StringComparison.OrdinalIgnoreCase))
                {
                    currentSection = "core";
                    currentElectiveGroup = null;
                    currentSpecialistGroup = null;
                    continue;
                }
                // "Elective units", "Elective Units", or "Electives" - may appear alone or combined with "Group A - Building" in same cell
                if (string.Equals(rowText, "Elective units", StringComparison.OrdinalIgnoreCase) || string.Equals(rowText, "Elective Units", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(rowText.Trim(), "Electives", StringComparison.OrdinalIgnoreCase)
                    || rowText.StartsWith("Elective units", StringComparison.OrdinalIgnoreCase) || rowText.StartsWith("Elective Units", StringComparison.OrdinalIgnoreCase)
                    || rowText.Trim().StartsWith("Electives", StringComparison.OrdinalIgnoreCase))
                {
                    currentSection = "elective";
                    currentElectiveGroup = null;
                    currentSpecialistGroup = null;
                    currentGeneralGroup = null;
                    // Don't continue - fall through to check for "Group A - Building" etc. in same row
                }
                // "General Electives" standalone section header (e.g. CPC40120) - switches to general section
                if (string.Equals(rowText.Trim(), "General Electives", StringComparison.OrdinalIgnoreCase))
                {
                    currentSection = "general";
                    currentElectiveGroup = null;
                    currentSpecialistGroup = null;
                    currentGeneralGroup = "GeneralElectives";
                    if (!generalGroupsMap.ContainsKey(currentGeneralGroup))
                    {
                        generalElectiveGroups.Add((currentGeneralGroup, "General Electives", new List<Dictionary<string, object>>()));
                        generalGroupsMap[currentGeneralGroup] = generalElectiveGroups.Count - 1;
                        generalOrderByGroup[currentGeneralGroup] = 1;
                    }
                    continue;
                }
                // Match hyphen (-), colon (:), en-dash (–), em-dash (—) for "Group B – Site Manager"
                var groupMatch = Regex.Match(rowText, @"Group\s+([A-Za-z0-9]+)\s*[-:\u2013\u2014]\s*(.+)", RegexOptions.IgnoreCase);
                // "Group A Measurements" (space between id and title, no hyphen/colon)
                var groupMatchSpace = Regex.Match(rowText, @"Group\s+([A-Za-z0-9]+)\s+(.+)", RegexOptions.IgnoreCase);
                // "Group A" or "Group B" (no title) - when in elective section, creates elective groups
                var groupNoTitleMatch = Regex.Match(rowText, @"^Group\s+([A-Za-z0-9]+)\s*$", RegexOptions.IgnoreCase);
                // Check specialist/general FIRST - "Elective units" + "Group A: Specialist elective units" in same cell must route to specialist, not elective
                if (groupMatch.Success && rowText.IndexOf("Specialist", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    currentSection = "specialist";
                    currentElectiveGroup = null;
                    currentSpecialistGroup = "Group" + groupMatch.Groups[1].Value.Trim();
                    var groupTitle = groupMatch.Groups[2].Value.Trim();
                    if (!specialistGroupsMap.ContainsKey(currentSpecialistGroup))
                    {
                        specialistElectiveGroups.Add((currentSpecialistGroup, groupTitle, new List<Dictionary<string, object>>()));
                        specialistGroupsMap[currentSpecialistGroup] = specialistElectiveGroups.Count - 1;
                        specialistOrderByGroup[currentSpecialistGroup] = 1;
                    }
                    continue;
                }
                // "Group B: General elective units" or "Group B - General Electives" (section header) - switches to general section. Not "Group A - General electives" (group title).
                if (groupMatch.Success && (rowText.IndexOf("General elective units", StringComparison.OrdinalIgnoreCase) >= 0
                    || rowText.IndexOf("General Electives", StringComparison.Ordinal) >= 0))
                {
                    currentSection = "general";
                    currentElectiveGroup = null;
                    currentSpecialistGroup = null;
                    currentGeneralGroup = "Group" + groupMatch.Groups[1].Value.Trim();
                    var groupTitle = groupMatch.Groups[2].Value.Trim();
                    if (!generalGroupsMap.ContainsKey(currentGeneralGroup))
                    {
                        generalElectiveGroups.Add((currentGeneralGroup, groupTitle, new List<Dictionary<string, object>>()));
                        generalGroupsMap[currentGeneralGroup] = generalElectiveGroups.Count - 1;
                        generalOrderByGroup[currentGeneralGroup] = 1;
                    }
                    continue;
                }
                // "Group A" or "Group B" (no title) - when in elective section, creates elective groups
                if (groupNoTitleMatch.Success && currentSection == "elective")
                {
                    currentElectiveGroup = "Group" + groupNoTitleMatch.Groups[1].Value.Trim();
                    if (!electiveGroupsMap.ContainsKey(currentElectiveGroup))
                    {
                        electiveGroups.Add((currentElectiveGroup, string.Empty, new List<Dictionary<string, object>>()));
                        electiveGroupsMap[currentElectiveGroup] = electiveGroups.Count - 1;
                        electiveOrderByGroup[currentElectiveGroup] = 1;
                    }
                    continue;
                }
                // When in elective section, "Group A - General electives" or "Group B - Plant operation" or "Group A - Building" creates elective groups
                if (groupMatch.Success && currentSection == "elective")
                {
                    currentElectiveGroup = "Group" + groupMatch.Groups[1].Value.Trim();
                    var groupTitle = groupMatch.Groups[2].Value.Trim();
                    if (!electiveGroupsMap.ContainsKey(currentElectiveGroup))
                    {
                        electiveGroups.Add((currentElectiveGroup, groupTitle, new List<Dictionary<string, object>>()));
                        electiveGroupsMap[currentElectiveGroup] = electiveGroups.Count - 1;
                        electiveOrderByGroup[currentElectiveGroup] = 1;
                    }
                    continue;
                }
                // "Group A Measurements" (space between id and title, no hyphen/colon) - when in elective section
                if (groupMatchSpace.Success && currentSection == "elective")
                {
                    currentElectiveGroup = "Group" + groupMatchSpace.Groups[1].Value.Trim();
                    var groupTitle = groupMatchSpace.Value.Trim(); // Keep full original text
                    if (!electiveGroupsMap.ContainsKey(currentElectiveGroup))
                    {
                        electiveGroups.Add((currentElectiveGroup, groupTitle, new List<Dictionary<string, object>>()));
                        electiveGroupsMap[currentElectiveGroup] = electiveGroups.Count - 1;
                        electiveOrderByGroup[currentElectiveGroup] = 1;
                    }
                    continue;
                }
                // "Other electives" - creates elective group when in elective section
                if (currentSection == "elective" && (string.Equals(rowTrimmed, "Other electives", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(rowTrimmed, "Other", StringComparison.OrdinalIgnoreCase)))
                {
                    currentElectiveGroup = "OtherElectives";
                    if (!electiveGroupsMap.ContainsKey(currentElectiveGroup))
                    {
                        electiveGroups.Add((currentElectiveGroup, rowTrimmed, new List<Dictionary<string, object>>()));
                        electiveGroupsMap[currentElectiveGroup] = electiveGroups.Count - 1;
                        electiveOrderByGroup[currentElectiveGroup] = 1;
                    }
                    continue;
                }
                // "Group X - Title" in specialist/general section without matching above - create new group in current section
                if (groupMatch.Success && currentSection != null)
                {
                    var groupKey = "Group" + groupMatch.Groups[1].Value.Trim();
                    var groupTitle = groupMatch.Groups[2].Value.Trim();
                    if (currentSection == "general")
                    {
                        currentGeneralGroup = groupKey;
                        if (!generalGroupsMap.ContainsKey(currentGeneralGroup))
                        {
                            generalElectiveGroups.Add((currentGeneralGroup, groupTitle, new List<Dictionary<string, object>>()));
                            generalGroupsMap[currentGeneralGroup] = generalElectiveGroups.Count - 1;
                            generalOrderByGroup[currentGeneralGroup] = 1;
                        }
                        continue;
                    }
                    if (currentSection == "specialist")
                    {
                        currentSpecialistGroup = groupKey;
                        if (!specialistGroupsMap.ContainsKey(currentSpecialistGroup))
                        {
                            specialistElectiveGroups.Add((currentSpecialistGroup, groupTitle, new List<Dictionary<string, object>>()));
                            specialistGroupsMap[currentSpecialistGroup] = specialistElectiveGroups.Count - 1;
                            specialistOrderByGroup[currentSpecialistGroup] = 1;
                        }
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

                // Inline prerequisite row: colspan=2 cell with "**Prerequisite unit CPCCWHS2001..." lines
                var prereqRowPatterns = new[] { "Prerequisite unit", "Prerequisiteunit", "Prerequisite_unit", "Prerequisite" };
                if (prereqRowPatterns.Any(p => rowText.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    var unitsForMatching = new List<(string code, string title, int asteriskCount)>(unitsWithAsteriskForPrereq);
                    foreach (var td in tds)
                    {
                        foreach (var p in td.Elements(ns + "p"))
                        {
                            var lineText = (CommonParser.ExtractInlineText(p) ?? string.Empty).Trim();
                            if (string.IsNullOrWhiteSpace(lineText)) continue;
                            var prereqMatch = Regex.Match(lineText, prereqLinePattern, RegexOptions.IgnoreCase);
                            if (!prereqMatch.Success) continue;
                            var prereqAsteriskCount = prereqMatch.Groups[1].Value.Length;
                            var prereqCode = prereqMatch.Groups[2].Value;
                            var prereqTitle = prereqMatch.Groups[3].Value.Trim().TrimEnd('.');
                            var idx = unitsForMatching.FindIndex(u => u.asteriskCount == prereqAsteriskCount);
                            if (idx >= 0)
                            {
                                var unitMatch = unitsForMatching[idx];
                                unitsForMatching.RemoveAt(idx);
                                var prereqOrder = inlinePrerequisites.Count + 1;
                                inlinePrerequisites.Add(new Dictionary<string, object>
                                {
                                    { "item_id", $"prerequisite_requirement-{prereqOrder}" },
                                    { "unit_of_competency", new Dictionary<string, object> { { "code", unitMatch.code }, { "title", unitMatch.title }, { "asterisk", unitMatch.asteriskCount } } },
                                    { "prerequisite_requirement", new List<Dictionary<string, object>> { new Dictionary<string, object> { { "code", prereqCode }, { "title", prereqTitle }, { "asterisk", 0 } } } }
                                });
                            }
                        }
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
                        // Parse prerequisites from third column when table has Prerequisites column (core or elective)
                        if (tds.Count >= 3 && ((coreHasPrerequisitesColumn && currentSection == "core") || (electiveHasPrerequisitesColumn && currentSection == "elective")))
                        {
                            var prereqList = ParsePrerequisitesFromCell(tds[2], ns, unitCodePattern);
                            if (prereqList != null && prereqList.Count > 0)
                            {
                                entry["prerequisites"] = prereqList;
                            }
                        }
                    }
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

        /// <summary>
        /// Parses prerequisite units from a table cell (e.g. Prerequisites column).
        /// Returns list of { code, title } or null if empty.
        /// </summary>
        private static List<Dictionary<string, object>> ParsePrerequisitesFromCell(XElement cell, XNamespace ns, string unitCodePattern)
        {
            var texts = new List<string>();
            foreach (var p in cell.Elements(ns + "p"))
            {
                var t = (CommonParser.ExtractInlineText(p) ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(t))
                    texts.Add(t);
            }
            if (texts.Count == 0)
            {
                var fullText = (CommonParser.ExtractInlineText(cell) ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(fullText))
                    return null;
                texts.AddRange(fullText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim()).Where(s => !string.IsNullOrWhiteSpace(s)));
            }
            var result = new List<Dictionary<string, object>>();
            for (var i = 0; i < texts.Count; i++)
            {
                var text = texts[i];
                var prereq = QualificationUnitHelper.ParseCodeAndTitleFromCell(text, unitCodePattern);
                if (prereq != null && !string.IsNullOrWhiteSpace(prereq["code"] as string))
                {
                    var title = (prereq["title"] as string) ?? string.Empty;
                    // If title is empty and next paragraph has no unit code, use it as title (code and title in separate paragraphs)
                    if (string.IsNullOrWhiteSpace(title) && i + 1 < texts.Count)
                    {
                        var nextText = texts[i + 1];
                        if (!Regex.IsMatch(nextText, unitCodePattern))
                        {
                            title = nextText.Trim();
                            i++; // Skip the title paragraph
                        }
                    }
                    result.Add(new Dictionary<string, object>
                    {
                        { "code", prereq["code"] },
                        { "title", title ?? string.Empty }
                    });
                }
            }
            return result.Count > 0 ? result : null;
        }

        private static (string title, int asterisk) NormalizeTitleAndAsterisk(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return (string.Empty, 0);
            }
            var t = title.Trim().TrimStart('-', ':', ' ');
            var asterisk = 0;
            var i = 0;
            while (i < t.Length && t[i] == '*')
            {
                asterisk++;
                i++;
            }
            if (asterisk > 0)
            {
                t = t.Substring(i).TrimStart(' ');
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
