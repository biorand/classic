using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IntelOrca.Biohazard.BioRand
{
    /// <summary>
    /// Builds formatted markdown where tables are easy to read when
    /// reading the raw markdown file.
    /// </summary>
    internal sealed class MarkdownBuilder
    {
        private readonly StringBuilder _sb = new();
        private readonly List<object[]> _tableRows = [];
        private bool _buildingTable;

        public string Build()
        {
            Checks();
            return _sb.ToString();
        }

        public void Heading(int level, string value)
        {
            Checks();

            Append('#', level);
            Append(' ');
            Append(value);
            AppendLine();
            AppendLine();
        }

        public void Table(params string[] headers)
        {
            Checks();

            _buildingTable = true;
            _tableRows.Clear();
            _tableRows.Add([.. headers]);
            _tableRows.Add([]);
        }

        public void TableRow(params object[] cells)
        {
            if (!_buildingTable)
                Table();

            _buildingTable = true;
            _tableRows.Add([.. cells]);
        }

        private void EndTable()
        {
            if (!_buildingTable)
                return;

            _buildingTable = false;

            var columnWidths = new List<int>();
            var maxColumns = _tableRows.Max(x => x.Length);
            while (columnWidths.Count < maxColumns)
                columnWidths.Add(0);
            foreach (var row in _tableRows)
            {
                var x = 0;
                foreach (var cell in row)
                {
                    columnWidths[x] = Math.Max(columnWidths[x], cell.ToString().Length);
                    x++;
                }
            }

            foreach (var row in _tableRows)
            {
                Append('|');
                if (row.Length == 0)
                {
                    for (var i = 0; i < columnWidths.Count; i++)
                    {
                        Append('-', columnWidths[i] + 2);
                        Append('|');
                    }
                }
                else
                {
                    var x = 0;
                    foreach (var cell in row)
                    {
                        var content = cell.ToString();
                        if (IsNumericType(cell))
                        {
                            // Right align
                            AppendSpace(columnWidths[x] - content.Length + 1);
                            Append(content);
                            AppendSpace();
                        }
                        else
                        {
                            // Left align
                            AppendSpace();
                            Append(content);
                            AppendSpace(columnWidths[x] - content.Length + 1);
                        }

                        Append('|');
                        x++;
                    }
                }
                AppendLine();
            }
            AppendLine();
        }

        private void Checks()
        {
            EndTable();
        }

        private void Append(char c) => _sb.Append(c);
        private void Append(char c, int repeat) => _sb.Append(c, repeat);
        private void Append(string value) => _sb.Append(value);
        private void AppendSpace(int count = 1) => _sb.Append(' ', count);
        public void AppendLine() => _sb.AppendLine();
        public void AppendLine(string value) => _sb.AppendLine(value);

        private static bool IsNumericType(object o)
        {
            return Type.GetTypeCode(o.GetType()) switch
            {
                TypeCode.Byte or
                TypeCode.SByte or
                TypeCode.UInt16 or
                TypeCode.UInt32 or
                TypeCode.UInt64 or
                TypeCode.Int16 or
                TypeCode.Int32 or
                TypeCode.Int64 or
                TypeCode.Decimal or
                TypeCode.Double or
                TypeCode.Single => true,
                _ => false,
            };
        }
    }
}
