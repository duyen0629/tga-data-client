using System.Collections.Generic;

namespace TgaGateway2.Handlers.TrainingComponentDocuments.Type
{

    internal abstract class TableRowBase
    {
    }

    internal sealed class ElementCriteriaRow : TableRowBase
    {
        public string element_id { get; set; }
        public string element_text { get; set; }
        public List<CriteriaItem> criteria { get; set; }
    }

    internal sealed class GenericTableRow : TableRowBase
    {
        public List<List<SectionItem>> cells { get; set; }
    }

    internal sealed class TextPart
    {
        public string text { get; set; }
        public bool bold { get; set; }
    }

    internal sealed class ElectiveRuleItem
    {
        public string item_id { get; set; }
        public string text { get; set; }
        public string type { get; set; }
        public int order { get; set; }
        public int? indent { get; set; }
        public string parent_bullet_item_id { get; set; }
        /// <summary>Inline content with bold parts when text has mixed formatting (e.g. "27 elective units" bold, ", of which:" not).</summary>
        public List<TextPart> content { get; set; }
    }

    internal sealed class SectionItem
    {
        public string item_id { get; set; }
        public string text { get; set; }
        public string type { get; set; }
        public int order { get; set; }
        public int? indent { get; set; }
        public string parent_bullet_item_id { get; set; }
    }

    internal sealed class CriteriaItem
    {
        public string id { get; set; }
        public string text { get; set; }
    }

    internal sealed class RowSpanCell
    {
        public RowSpanCell(List<SectionItem> items, int remainingRows)
        {
            Items = items;
            RemainingRows = remainingRows;
        }

        public List<SectionItem> Items { get; }
        public int RemainingRows { get; set; }
    }

    internal sealed class SectionTable
    {
        public List<string> columns { get; set; }
        public List<TableRowBase> rows { get; set; }
        public int? order { get; set; }
    }

    internal sealed class DocumentSection
    {
        private readonly List<string> _contentLines = new List<string>();
        private readonly List<string> _tableLines = new List<string>();

        public string key { get; set; }
        public string title { get; set; }
        public int order { get; set; }
        public List<SectionItem> items { get; set; } = new List<SectionItem>();
        public SectionTable table { get; set; }

        public void AddContentLine(string line) => _contentLines.Add(line);
        public void AddTableLine(string line) => _tableLines.Add(line);
        public List<string> GetContentLines() => _contentLines;
        public List<string> GetTableLines() => _tableLines;
    }

    internal sealed class ReleaseFileInfo
    {
        public string XmlPath { get; set; }
    }

    internal sealed class ReleaseFileSelection
    {
        public string ReleaseNumber { get; set; }
        public ReleaseFileInfo Complete { get; set; }
    }

    internal sealed class LoadedLinesResult
    {
        public LoadedLinesResult(List<string> lines, string formatUsed, string selectedRelativePath, byte[] bytes)
        {
            Lines = lines ?? new List<string>();
            FormatUsed = formatUsed;
            SelectedRelativePath = selectedRelativePath;
            Bytes = bytes;
        }

        public List<string> Lines { get; }
        public string FormatUsed { get; }
        public string SelectedRelativePath { get; }
        public byte[] Bytes { get; }
    }
}
