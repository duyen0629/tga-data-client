using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace TgaGateway2.Handlers.TrainingComponentDocuments.Parser
{
    // Parses prerequisite requirements table from qualification documents.
    internal static class QualificationPrerequisiteRequirementParser
    {
        /// <summary>
        /// Returns true if the table has prerequisite requirement structure (unit in qualification | prerequisite unit).
        /// </summary>
        internal static bool IsPrerequisiteRequirementsTable(XElement table, XNamespace ns)
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
            // Column 1: unit in qualification (e.g. "Unit of competency" or "UNIT IN THIS QUALIFICATION")
            var col0IsUnit = c0.IndexOf("Unit of competency", StringComparison.OrdinalIgnoreCase) >= 0
                || (c0.IndexOf("unit", StringComparison.OrdinalIgnoreCase) >= 0 && c0.IndexOf("qualification", StringComparison.OrdinalIgnoreCase) >= 0);
            // Column 2: prerequisite (e.g. "Prerequisite requirement" or "PREREQUISITE UNIT")
            var col1IsPrereq = c1.IndexOf("Prerequisite requirement", StringComparison.OrdinalIgnoreCase) >= 0
                || (c1.IndexOf("prerequisite", StringComparison.OrdinalIgnoreCase) >= 0 && c1.IndexOf("unit", StringComparison.OrdinalIgnoreCase) >= 0);
            return col0IsUnit && col1IsPrereq;
        }

        internal static void Parse(XElement table, XNamespace ns, Dictionary<string, object> packagingRules)
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
                    continue;
                }

                var cell0Text = CommonParser.ExtractInlineText(tds[0]).Trim();
                var cell1Text = CommonParser.ExtractInlineText(tds[1]).Trim();
                var unitOfCompetency = QualificationUnitHelper.ParseCodeAndTitleFromCell(cell0Text, unitCodePattern)
                    ?? new Dictionary<string, object> { { "code", string.Empty }, { "title", cell0Text }, { "asterisk", false } };
                var prerequisiteRequirement = QualificationUnitHelper.ParseCodeAndTitleFromCell(cell1Text, unitCodePattern);

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
                    { "prerequisite_requirement", prerequisiteRequirement },
                    { "item_id", $"prerequisite_requirement-{order++}" }
                });
            }

            packagingRules["prerequisite_requirements"] = prerequisiteList;
        }
    }
}
