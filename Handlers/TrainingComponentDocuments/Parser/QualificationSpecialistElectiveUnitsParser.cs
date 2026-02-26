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
        /// specialistElectiveUnits: list of groups with key, title, category, items; or single group when ungrouped.
        /// generalElectiveUnits: list of groups with category, title, items (category from XML section header).
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

            string specialistCategory = null;
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
                    specialistCategory = text;
                    break;
                }
            }

            if (specialistStartIndex < 0)
            {
                return (new List<object>(), new List<Dictionary<string, object>>());
            }

            if (string.IsNullOrEmpty(specialistCategory))
            {
                specialistCategory = "Specialist Elective Units";
            }
            string generalCategory = null;
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
                    generalCategory = text.Trim();
                    currentGroup = null;
                    currentGroupTitle = null;
                    continue;
                }

                // Single pattern: "Group A", "Group A: Title", "Group A Copper Cabling", "Group A - Building"
                // Title = full text; key = Group + id for grouping and item_ids
                var groupMatch = Regex.Match(text, @"^Group\s+([A-Za-z0-9]+)\s*(.*)$", RegexOptions.IgnoreCase);
                if (groupMatch.Success)
                {
                    var rest = groupMatch.Groups[2].Value.Trim();
                    if (string.IsNullOrEmpty(rest) || !Regex.IsMatch(rest, @"^[A-Z]{2,10}\d{2,6}"))
                    {
                        inGeneralElectiveUnits = false;
                        currentGroup = "Group" + groupMatch.Groups[1].Value.Trim();
                        currentGroupTitle = text.Trim();
                        if (!specialistGroupsMap.ContainsKey(currentGroup))
                        {
                            specialistGroupsOrdered.Add((currentGroup, currentGroupTitle, new List<Dictionary<string, object>>()));
                            specialistGroupsMap[currentGroup] = specialistGroupsOrdered.Count - 1;
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
                            { "category", specialistCategory },
                            { "items", g.items }
                        });
                    }
                }
            }
            else if (specialistUngroupedUnits.Count > 0)
            {
                specialistResult.Add(new Dictionary<string, object>
                {
                    { "category", specialistCategory },
                    { "title", specialistCategory },
                    { "items", specialistUngroupedUnits }
                });
            }

            var generalResult = new List<Dictionary<string, object>>();
            if (generalElectiveUnits.Count > 0 && !string.IsNullOrEmpty(generalCategory))
            {
                generalResult.Add(new Dictionary<string, object>
                {
                    { "category", generalCategory },
                    { "title", generalCategory },
                    { "items", generalElectiveUnits }
                });
            }
            else if (generalElectiveUnits.Count > 0)
            {
                generalResult.Add(new Dictionary<string, object>
                {
                    { "category", "General Elective Units" },
                    { "title", "General Elective Units" },
                    { "items", generalElectiveUnits }
                });
            }

            return (specialistResult, generalResult);
        }
    }
}
