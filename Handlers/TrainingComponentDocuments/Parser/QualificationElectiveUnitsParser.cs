using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace TgaGateway2.Handlers.TrainingComponentDocuments.Parser
{
    /// <summary>
    /// Parses "Elective units" section from paragraph format (Group A, Group B, etc.).
    /// Used when elective units are in paragraphs rather than tables.
    /// </summary>
    internal static class QualificationElectiveUnitsParser
    {
        /// <summary>
        /// Returns elective units from "Elective units" section in paragraph format.
        /// Format: array of { key, title, items } when groups, or flat array of units.
        /// </summary>
        internal static List<object> ParseFromParagraphs(
            List<XElement> children,
            XNamespace ns,
            string unitCodePattern)
        {
            var electiveGroupsOrdered = new List<(string key, string title, List<Dictionary<string, object>> items)>();
            var electiveGroupsMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var ungroupedUnits = new List<Dictionary<string, object>>();
            var electiveStartIndex = -1;
            string electiveCategory = null;

            for (var i = 0; i < children.Count; i++)
            {
                if (children[i].Name != ns + "p")
                {
                    continue;
                }
                var text = (CommonParser.ExtractInlineText(children[i]) ?? string.Empty).Trim();
                if (string.Equals(text, "Elective units", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "Electives", StringComparison.OrdinalIgnoreCase))
                {
                    electiveStartIndex = i;
                    electiveCategory = string.Equals(text, "Elective units", StringComparison.OrdinalIgnoreCase) ? "Elective units" : "Electives";
                    break;
                }
            }

            if (electiveStartIndex < 0)
            {
                return new List<object>();
            }

            if (string.IsNullOrEmpty(electiveCategory))
            {
                electiveCategory = "Elective units";
            }

            string currentGroup = null;
            string currentGroupTitle = null;
            var orderByGroup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var ungroupedOrder = 1;

            for (var i = electiveStartIndex + 1; i < children.Count; i++)
            {
                var node = children[i];
                if (node.Name == ns + "table")
                {
                    break;
                }
                if (node.Name != ns + "p")
                {
                    continue;
                }

                var text = (CommonParser.ExtractInlineText(node) ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                // Stop at "Core Units" or "Specialist Elective Units" or "General Elective Units"
                if (string.Equals(text, "Core Units", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(text, "Specialist Elective Units", StringComparison.OrdinalIgnoreCase)
                    || text.IndexOf("General Elective Units", StringComparison.OrdinalIgnoreCase) >= 0
                    || text.IndexOf("General Electives", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    break;
                }

                // Single pattern: "Group A", "Group A: Copper Cabling", "Group A Copper Cabling", "Group A - Building"
                // Title = full text; key = Group + id for grouping and item_ids
                var groupMatch = Regex.Match(text, @"^Group\s+([A-Za-z0-9]+)\s*(.*)$", RegexOptions.IgnoreCase);
                if (groupMatch.Success)
                {
                    var rest = groupMatch.Groups[2].Value.Trim();
                    // Don't treat as group if rest starts with unit code (e.g. "Group A ICTCBL254..." would be malformed)
                    if (string.IsNullOrEmpty(rest) || !Regex.IsMatch(rest, @"^[A-Z]{2,10}\d{2,6}"))
                    {
                        currentGroup = "Group" + groupMatch.Groups[1].Value.Trim();
                        currentGroupTitle = text.Trim();
                        if (!electiveGroupsMap.ContainsKey(currentGroup))
                        {
                            electiveGroupsOrdered.Add((currentGroup, currentGroupTitle, new List<Dictionary<string, object>>()));
                            electiveGroupsMap[currentGroup] = electiveGroupsOrdered.Count - 1;
                            orderByGroup[currentGroup] = 1;
                        }
                        continue;
                    }
                }

                var unitEntry = QualificationUnitHelper.ParseCodeAndTitleFromCell(text, unitCodePattern);
                if (unitEntry == null)
                {
                    continue;
                }

                if (currentGroup != null)
                {
                    var idx = electiveGroupsMap[currentGroup];
                    var order = orderByGroup[currentGroup]++;
                    unitEntry["item_id"] = $"elective_unit_{currentGroup}-{order}";
                    electiveGroupsOrdered[idx].items.Add(unitEntry);
                }
                else
                {
                    unitEntry["item_id"] = $"elective_unit-{ungroupedOrder++}";
                    ungroupedUnits.Add(unitEntry);
                }
            }

            var result = new List<object>();
            if (electiveGroupsOrdered.Count > 0)
            {
                foreach (var g in electiveGroupsOrdered)
                {
                    if (g.items.Count > 0)
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
            }
            else if (ungroupedUnits.Count > 0)
            {
                result.Add(new Dictionary<string, object>
                {
                    { "category", electiveCategory },
                    { "title", electiveCategory },
                    { "items", ungroupedUnits }
                });
            }

            return result;
        }
    }
}
