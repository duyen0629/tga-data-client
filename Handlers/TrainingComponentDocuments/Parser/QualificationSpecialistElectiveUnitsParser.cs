using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace TgaGateway2.Handlers.TrainingComponentDocuments.Parser
{
    internal static class QualificationSpecialistElectiveUnitsParser
    {
        /// <summary>
        /// Returns (specialistElectiveUnits, generalElectiveUnits).
        /// specialistElectiveUnits: list of groups with key, title, items; or flat list of units when no groups.
        /// generalElectiveUnits: flat list of units (no groups).
        /// </summary>
        internal static (List<object> specialistElectiveUnits, List<Dictionary<string, object>> generalElectiveUnits) Parse(
            List<XElement> children,
            XNamespace ns,
            string unitCodePattern)
        {
            var specialistGroupsOrdered = new List<(string key, string title, List<Dictionary<string, object>> items)>();
            var specialistGroupsMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var specialistUngroupedUnits = new List<Dictionary<string, object>>();
            var generalElectiveUnits = new List<Dictionary<string, object>>();
            var specialistStartIndex = -1;

            for (var i = 0; i < children.Count; i++)
            {
                if (children[i].Name != ns + "p")
                {
                    continue;
                }
                var text = (CommonParser.ExtractInlineText(children[i]) ?? string.Empty).Trim();
                if (string.Equals(text, "Specialist Elective Units", StringComparison.OrdinalIgnoreCase))
                {
                    specialistStartIndex = i;
                    break;
                }
            }

            if (specialistStartIndex < 0)
            {
                return (new List<object>(), generalElectiveUnits);
            }

            string currentGroup = null;
            string currentGroupTitle = null;
            var orderByGroup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var specialistOrder = 1;
            var inGeneralElectiveUnits = false;
            var generalOrder = 1;

            for (var i = specialistStartIndex + 1; i < children.Count; i++)
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

                // "General Elective Units" starts a separate section - units go to general_elective_units
                if (text.IndexOf("General Elective Units", StringComparison.OrdinalIgnoreCase) >= 0
                    || text.IndexOf("General Electives", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    inGeneralElectiveUnits = true;
                    currentGroup = null;
                    currentGroupTitle = null;
                    continue;
                }

                // Group pattern with title: "Group A: Mobile Plant Equipment"
                var groupWithTitleMatch = Regex.Match(text, @"^Group\s+([A-Za-z0-9]+)\s*:\s*(.+)$", RegexOptions.IgnoreCase);
                if (groupWithTitleMatch.Success)
                {
                    inGeneralElectiveUnits = false;
                    currentGroup = "Group" + groupWithTitleMatch.Groups[1].Value.Trim();
                    currentGroupTitle = groupWithTitleMatch.Groups[2].Value.Trim();
                    if (!specialistGroupsMap.ContainsKey(currentGroup))
                    {
                        specialistGroupsOrdered.Add((currentGroup, currentGroupTitle, new List<Dictionary<string, object>>()));
                        specialistGroupsMap[currentGroup] = specialistGroupsOrdered.Count - 1;
                        orderByGroup[currentGroup] = 1;
                    }
                    continue;
                }

                // Group pattern without title: "Group A"
                var groupNoTitleMatch = Regex.Match(text, @"^Group\s+([A-Za-z0-9]+)\s*$", RegexOptions.IgnoreCase);
                if (groupNoTitleMatch.Success)
                {
                    inGeneralElectiveUnits = false;
                    currentGroup = "Group" + groupNoTitleMatch.Groups[1].Value.Trim();
                    currentGroupTitle = string.Empty;
                    if (!specialistGroupsMap.ContainsKey(currentGroup))
                    {
                        specialistGroupsOrdered.Add((currentGroup, currentGroupTitle, new List<Dictionary<string, object>>()));
                        specialistGroupsMap[currentGroup] = specialistGroupsOrdered.Count - 1;
                        orderByGroup[currentGroup] = 1;
                    }
                    continue;
                }

                var unitEntry = QualificationUnitHelper.ParseCodeAndTitleFromCell(text, unitCodePattern);
                if (unitEntry == null)
                {
                    continue;
                }

                if (inGeneralElectiveUnits)
                {
                    unitEntry["item_id"] = $"general_elective_unit-{generalOrder++}";
                    generalElectiveUnits.Add(unitEntry);
                }
                else if (currentGroup != null)
                {
                    var idx = specialistGroupsMap[currentGroup];
                    var order = orderByGroup[currentGroup]++;
                    unitEntry["item_id"] = $"specialist_elective_unit_{currentGroup}-{order}";
                    specialistGroupsOrdered[idx].items.Add(unitEntry);
                }
                else
                {
                    unitEntry["item_id"] = $"specialist_elective_unit-{specialistOrder++}";
                    specialistUngroupedUnits.Add(unitEntry);
                }
            }

            var specialistResult = new List<object>();
            if (specialistGroupsOrdered.Count > 0)
            {
                foreach (var g in specialistGroupsOrdered)
                {
                    if (g.items.Count > 0)
                    {
                        specialistResult.Add(new Dictionary<string, object>
                        {
                            { "key", g.key },
                            { "title", g.title },
                            { "items", g.items }
                        });
                    }
                }
            }
            else if (specialistUngroupedUnits.Count > 0)
            {
                specialistResult.AddRange(specialistUngroupedUnits.Cast<object>());
            }

            return (specialistResult, generalElectiveUnits);
        }
    }
}
