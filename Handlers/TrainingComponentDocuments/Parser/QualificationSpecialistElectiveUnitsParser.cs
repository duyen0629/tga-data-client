using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace TgaGateway2.Handlers.TrainingComponentDocuments.Parser
{
    internal static class QualificationSpecialistElectiveUnitsParser
    {
        internal static (Dictionary<string, List<Dictionary<string, object>>> specialistElectiveUnits, List<Dictionary<string, object>> generalElectiveUnits) Parse(
            List<XElement> children,
            XNamespace ns,
            string unitCodePattern)
        {
            var specialistGroups = new Dictionary<string, List<Dictionary<string, object>>>(StringComparer.OrdinalIgnoreCase);
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
                return (specialistGroups, generalElectiveUnits);
            }

            string currentGroup = null;
            var orderByGroup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
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
                    continue;
                }

                // Group pattern: "Group A: Heavy Vehicle Manual Transmission"
                var groupMatch = Regex.Match(text, @"^Group\s+([A-Za-z0-9]+)\s*:", RegexOptions.IgnoreCase);
                if (groupMatch.Success)
                {
                    inGeneralElectiveUnits = false;
                    currentGroup = "Group" + groupMatch.Groups[1].Value.Trim();
                    if (!specialistGroups.ContainsKey(currentGroup))
                    {
                        specialistGroups[currentGroup] = new List<Dictionary<string, object>>();
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
                    var order = orderByGroup[currentGroup]++;
                    unitEntry["item_id"] = $"specialist_elective_unit_{currentGroup}-{order}";
                    specialistGroups[currentGroup].Add(unitEntry);
                }
            }

            return (specialistGroups, generalElectiveUnits);
        }
    }
}
