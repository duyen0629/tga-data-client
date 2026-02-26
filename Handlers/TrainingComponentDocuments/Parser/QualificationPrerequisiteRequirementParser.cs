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
        /// Handles tables with a title row (colspan=2) followed by a header row with two columns.
        /// </summary>
        internal static bool IsPrerequisiteRequirementsTable(XElement table, XNamespace ns)
        {
            var rows = table.Elements(ns + "tr").ToList();
            if (rows.Count == 0)
            {
                return false;
            }
            // Find first row with 2+ cells (skip title row like "Prerequisite unit requirements in this qualification")
            XElement headerRow = null;
            foreach (var row in rows)
            {
                var cells = row.Elements(ns + "td").Concat(row.Elements(ns + "th")).ToList();
                if (cells.Count >= 2)
                {
                    headerRow = row;
                    break;
                }
            }
            if (headerRow == null)
            {
                return false;
            }
            var headerCells = headerRow.Elements(ns + "td").Concat(headerRow.Elements(ns + "th")).ToList();
            if (headerCells.Count < 2)
            {
                return false;
            }
            var c0 = CommonParser.ExtractInlineText(headerCells[0]).Trim();
            var c1 = CommonParser.ExtractInlineText(headerCells[1]).Trim();
            // Column 1: unit in qualification (e.g. "Unit of competency", "Unit in this qualification")
            var col0IsUnit = c0.IndexOf("Unit of competency", StringComparison.OrdinalIgnoreCase) >= 0
                || (c0.IndexOf("unit", StringComparison.OrdinalIgnoreCase) >= 0 && c0.IndexOf("qualification", StringComparison.OrdinalIgnoreCase) >= 0);
            // Column 2: prerequisite (e.g. "Prerequisite requirement", "Prerequisite unit")
            var col1IsPrereq = c1.IndexOf("Prerequisite requirement", StringComparison.OrdinalIgnoreCase) >= 0
                || (c1.IndexOf("prerequisite", StringComparison.OrdinalIgnoreCase) >= 0 && c1.IndexOf("unit", StringComparison.OrdinalIgnoreCase) >= 0);
            return col0IsUnit && col1IsPrereq;
        }

        internal static void Parse(XElement table, XNamespace ns, Dictionary<string, object> packagingRules)
        {
            const string unitCodePattern = @"\b([A-Z]{2,10}\d{2,6}[A-Z]?)\b";
            var rows = table.Elements(ns + "tr").ToList();
            var headerRowIndices = new HashSet<int>();
            // Skip title row (1 cell) and header row (2 cells with "Unit in qualification" | "Prerequisite unit")
            for (var idx = 0; idx < rows.Count; idx++)
            {
                var row = rows[idx];
                var cells = row.Elements(ns + "td").Concat(row.Elements(ns + "th")).ToList();
                if (cells.Count < 2)
                {
                    headerRowIndices.Add(idx);
                    continue;
                }
                var c0 = CommonParser.ExtractInlineText(cells[0]).Trim();
                var c1 = CommonParser.ExtractInlineText(cells[1]).Trim();
                var col0IsHeader = c0.IndexOf("Unit of competency", StringComparison.OrdinalIgnoreCase) >= 0
                    || (c0.IndexOf("unit", StringComparison.OrdinalIgnoreCase) >= 0 && c0.IndexOf("qualification", StringComparison.OrdinalIgnoreCase) >= 0);
                var col1IsHeader = c1.IndexOf("Prerequisite requirement", StringComparison.OrdinalIgnoreCase) >= 0
                    || (c1.IndexOf("prerequisite", StringComparison.OrdinalIgnoreCase) >= 0 && c1.IndexOf("unit", StringComparison.OrdinalIgnoreCase) >= 0);
                if (col0IsHeader && col1IsHeader)
                {
                    headerRowIndices.Add(idx);
                }
            }

            var prerequisiteList = new List<Dictionary<string, object>>();
            var order = 1;
            for (var i = 0; i < rows.Count; i++)
            {
                if (headerRowIndices.Contains(i))
                {
                    continue;
                }

                var tds = rows[i].Elements(ns + "td").ToList();
                if (tds.Count < 2)
                {
                    continue;
                }

                var cell0Text = CommonParser.ExtractInlineText(tds[0]).Trim();
                var unitOfCompetency = QualificationUnitHelper.ParseCodeAndTitleFromCell(cell0Text, unitCodePattern)
                    ?? new Dictionary<string, object> { { "code", string.Empty }, { "title", cell0Text }, { "asterisk", 0 } };

                // Cell 1 may have multiple <p> elements (one prerequisite per paragraph)
                var prerequisiteParagraphs = tds[1].Elements(ns + "p").ToList();
                var prerequisiteTexts = prerequisiteParagraphs.Count > 0
                    ? prerequisiteParagraphs.Select(p => CommonParser.ExtractInlineText(p).Trim()).Where(t => !string.IsNullOrWhiteSpace(t)).ToList()
                    : new List<string> { CommonParser.ExtractInlineText(tds[1]).Trim() };

                if (prerequisiteTexts.Count == 0)
                {
                    continue;
                }

                bool unitTitleEmptyOrSymbol = string.IsNullOrWhiteSpace(unitOfCompetency["title"]?.ToString())
                    || unitOfCompetency["title"]?.ToString().Trim() == "*";
                var isFirstPrereq = true;
                var prerequisiteRequirements = new List<Dictionary<string, object>>();

                foreach (var prereqText in prerequisiteTexts)
                {
                    var prerequisiteRequirement = QualificationUnitHelper.ParseCodeAndTitleFromCell(prereqText, unitCodePattern);
                    if (unitTitleEmptyOrSymbol && isFirstPrereq && !string.IsNullOrWhiteSpace(prereqText) && prerequisiteRequirement == null)
                    {
                        unitOfCompetency["title"] = prereqText;
                        prerequisiteRequirement = new Dictionary<string, object> { { "code", string.Empty }, { "title", string.Empty }, { "asterisk", 0 } };
                    }
                    isFirstPrereq = false;
                    if (prerequisiteRequirement == null)
                    {
                        prerequisiteRequirement = new Dictionary<string, object> { { "code", string.Empty }, { "title", prereqText }, { "asterisk", 0 } };
                    }

                    prerequisiteRequirements.Add(prerequisiteRequirement);
                }

                prerequisiteList.Add(new Dictionary<string, object>
                {
                    { "unit_of_competency", unitOfCompetency },
                    { "prerequisite_requirement", prerequisiteRequirements },
                    { "item_id", $"prerequisite_requirement-{order++}" }
                });
            }

            packagingRules["prerequisite_requirements"] = prerequisiteList;
        }
    }
}
