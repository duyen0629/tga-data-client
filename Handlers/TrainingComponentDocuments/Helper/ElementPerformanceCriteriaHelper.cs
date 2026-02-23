using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using TgaGateway2.Handlers.TrainingComponentDocuments.Parser;
using TgaGateway2.Handlers.TrainingComponentDocuments.Type;

namespace TgaGateway2.Handlers.TrainingComponentDocuments.Helper
{
    /// <summary>
    /// Parses "Elements and Performance Criteria" tables from topic XML into SectionTable (ElementCriteriaRow).
    /// </summary>
    internal static class ElementPerformanceCriteriaHelper
    {
        internal static bool IsElementsPerformanceCriteriaSection(string sectionKey)
        {
            return SectionKeyHelper.SectionKeyEquals(sectionKey, "elements_and_performance_criteria") ||
                   SectionKeyHelper.SectionKeyEquals(sectionKey, "elements_performance_criteria");
        }

        internal static SectionTable ParseElementsTableFromXml(XElement topic, XNamespace ns)
        {
            var table = topic.Element(ns + "Text")?.Element(ns + "table");
            var rows = new List<ElementCriteriaRow>();
            if (table == null)
            {
                return new SectionTable { columns = new List<string> { "Element", "Performance Criteria" }, rows = new List<TableRowBase>() };
            }

            ElementCriteriaRow currentRow = null;

            foreach (var tr in table.Elements(ns + "tr"))
            {
                var tds = tr.Elements(ns + "td").ToList();
                if (tds.Count < 2)
                {
                    continue;
                }

                var cellTexts = tds.Select(CommonParser.ExtractInlineText).Select(t => t.Trim()).ToList();

                string elementNo = null;
                string elementText = null;
                string criteriaNo = null;
                string criteriaText = null;

                if (cellTexts.Any(t => string.Equals(t, "ELEMENTS", StringComparison.OrdinalIgnoreCase)) ||
                    cellTexts.Any(t => string.Equals(t, "ELEMENT", StringComparison.OrdinalIgnoreCase)) ||
                    cellTexts.Any(t => string.Equals(t, "Element", StringComparison.OrdinalIgnoreCase)) ||
                    cellTexts.Any(t => string.Equals(t, "PERFORMANCE CRITERIA", StringComparison.OrdinalIgnoreCase)) ||
                    cellTexts.Any(t => string.Equals(t, "Performance Criteria", StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrWhiteSpace(cellTexts.ElementAtOrDefault(0)) && cellTexts[0].StartsWith("Elements describe", StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrWhiteSpace(cellTexts.ElementAtOrDefault(1)) && cellTexts[1].StartsWith("Performance criteria describe", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                if (tds.Count == 2)
                {
                    var firstCellText = cellTexts[0];
                    var criteriaOnlyMatch = Regex.Match(firstCellText, @"^\s*(\d+\.\d+)\s*\.?\s*$");
                    if (criteriaOnlyMatch.Success)
                    {
                        criteriaNo = firstCellText;
                        criteriaText = cellTexts[1];
                    }
                    else
                    {
                        var elementCellText = firstCellText;
                        var elementMatch = Regex.Match(elementCellText, @"^\s*(\d+)\s*\.?\s*(.+)$");
                        var elementNumber = elementMatch.Success ? elementMatch.Groups[1].Value : (rows.Count + 1).ToString();
                        var elementDisplayText = elementMatch.Success ? elementMatch.Groups[2].Value.Trim() : elementCellText;

                        currentRow = new ElementCriteriaRow
                        {
                            element_id = $"element-{elementNumber.Replace(".", "_").Replace("-", "_")}",
                            element_text = string.IsNullOrWhiteSpace(elementDisplayText)
                                ? elementNumber
                                : $"{elementNumber} {elementDisplayText}".Trim(),
                            criteria = new List<CriteriaItem>()
                        };
                        rows.Add(currentRow);

                        var criteriaParagraphs = tds[1]
                            .Descendants(ns + "p")
                            .Select(CommonParser.ExtractInlineText)
                            .Where(t => !string.IsNullOrWhiteSpace(t))
                            .ToList();

                        foreach (var criteriaLine in criteriaParagraphs)
                        {
                            var criteriaMatch = Regex.Match(criteriaLine, @"^\s*(\d+\.\d+)\.?\s*(.+)$");
                            var criteriaNumber = criteriaMatch.Success
                                ? criteriaMatch.Groups[1].Value
                                : $"{elementNumber}.{currentRow.criteria.Count + 1}";
                            var criteriaDisplayText = criteriaMatch.Success ? criteriaMatch.Groups[2].Value.Trim() : criteriaLine.Trim();

                            if (!string.IsNullOrWhiteSpace(criteriaDisplayText) &&
                                criteriaDisplayText.StartsWith(criteriaNumber + " ", StringComparison.Ordinal))
                            {
                                criteriaDisplayText = criteriaDisplayText.Substring(criteriaNumber.Length).Trim();
                            }

                            currentRow.criteria.Add(new CriteriaItem
                            {
                                id = $"criteria-{criteriaNumber.Replace(".", "_").Replace("-", "_")}",
                                text = string.IsNullOrWhiteSpace(criteriaDisplayText)
                                    ? criteriaNumber
                                    : $"{criteriaNumber} {criteriaDisplayText}"
                            });
                        }

                        continue;
                    }
                }

                if (tds.Count >= 4)
                {
                    elementNo = cellTexts[0];
                    elementText = cellTexts[1];
                    criteriaNo = cellTexts[2];
                    criteriaText = cellTexts[3];
                }
                else
                {
                    criteriaNo = cellTexts[0];
                    criteriaText = cellTexts[1];
                }

                if (!string.IsNullOrWhiteSpace(elementNo) || !string.IsNullOrWhiteSpace(elementText))
                {
                    var elementNumber = elementNo;
                    if (string.IsNullOrWhiteSpace(elementNumber))
                    {
                        var match = Regex.Match(elementText, @"^\d+");
                        elementNumber = match.Success ? match.Value : (rows.Count + 1).ToString();
                    }

                    var elementDisplayText = elementText;
                    if (string.IsNullOrWhiteSpace(elementDisplayText) && !string.IsNullOrWhiteSpace(elementNo))
                    {
                        elementDisplayText = elementNo;
                    }

                    currentRow = new ElementCriteriaRow
                    {
                        element_id = $"element-{elementNumber.Replace(".", "_").Replace("-", "_")}",
                        element_text = string.IsNullOrWhiteSpace(elementDisplayText)
                            ? elementNumber
                            : $"{elementNumber} {elementDisplayText}",
                        criteria = new List<CriteriaItem>()
                    };
                    rows.Add(currentRow);
                }

                if (!string.IsNullOrWhiteSpace(criteriaText) || !string.IsNullOrWhiteSpace(criteriaNo))
                {
                    if (currentRow == null)
                    {
                        currentRow = new ElementCriteriaRow
                        {
                            element_id = "element-unknown",
                            element_text = "Unknown",
                            criteria = new List<CriteriaItem>()
                        };
                        rows.Add(currentRow);
                    }

                    var criteriaNumber = criteriaNo;
                    if (string.IsNullOrWhiteSpace(criteriaNumber))
                    {
                        var match = Regex.Match(criteriaText, @"^\d+\.\d+");
                        criteriaNumber = match.Success ? match.Value : $"{currentRow.criteria.Count + 1}";
                    }

                    var criteriaDisplayText = criteriaText;
                    if (!string.IsNullOrWhiteSpace(criteriaDisplayText) && criteriaDisplayText.StartsWith(criteriaNumber + " ", StringComparison.Ordinal))
                    {
                        criteriaDisplayText = criteriaDisplayText.Substring(criteriaNumber.Length).Trim();
                    }
                    currentRow.criteria.Add(new CriteriaItem
                    {
                        id = $"criteria-{criteriaNumber.Replace(".", "_").Replace("-", "_")}",
                        text = string.IsNullOrWhiteSpace(criteriaDisplayText)
                            ? criteriaNumber
                            : $"{criteriaNumber} {criteriaDisplayText}"
                    });
                }
            }

            return new SectionTable
            {
                columns = new List<string> { "Element", "Performance Criteria" },
                rows = rows.Cast<TableRowBase>().ToList()
            };
        }
    }
}
