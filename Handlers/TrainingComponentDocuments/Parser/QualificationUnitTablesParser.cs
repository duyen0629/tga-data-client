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
            bool foundCoreTable) ParseCoreAndElectiveUnitsFromTables(
            List<XElement> children,
            XNamespace ns,
            string unitCodePattern)
        {
            var electiveGroupsOrdered = new List<(string key, string title, List<Dictionary<string, object>> items)>();
            var electiveGroupsMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
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

            return (coreUnits, electiveResult, foundCoreTable);
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
