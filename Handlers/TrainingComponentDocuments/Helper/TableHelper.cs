using System.Linq;
using System.Xml.Linq;

namespace TgaGateway2.Handlers.TrainingComponentDocuments.Helper
{
    /// <summary>
    /// Helpers for reading values from table cell XML (e.g. colspan, rowspan) and table structure.
    /// </summary>
    internal static class TableHelper
    {
        internal static int GetTableMaxColumnsFromTable(XElement table, XNamespace ns)
        {
            if (table == null)
            {
                return 0;
            }

            var maxColumns = 0;
            foreach (var tr in table.Elements(ns + "tr"))
            {
                var tdCount = tr.Elements(ns + "td").Count();
                if (tdCount > maxColumns)
                {
                    maxColumns = tdCount;
                }
            }

            return maxColumns;
        }

        internal static int GetTableRowCountFromTable(XElement table, XNamespace ns)
        {
            if (table == null)
            {
                return 0;
            }

            return table.Elements(ns + "tr").Count();
        }

        internal static int GetSpanValue(XElement td, string attributeName)
        {
            if (td == null)
            {
                return 1;
            }

            var value = td.Attribute(attributeName)?.Value;
            return int.TryParse(value, out var span) && span > 0 ? span : 1;
        }
    }
}
