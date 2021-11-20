using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorerCore.Unreal.Classes
{
    public class Bio2DA
    {
        public bool IsIndexed = false;

        public Bio2DACell[,] Cells { get; set; }

        private List<string> _rowNames;

        /// <summary>
        /// List of raw rownames
        /// </summary>
        public IReadOnlyList<string> RowNames => _rowNames;

        private List<string> _columnNames;
        /// <summary>
        /// List of column names
        /// </summary>
        public IReadOnlyList<string> ColumnNames => _columnNames;

        /// <summary>
        /// Replaces _ with __ to avoid AccessKeys when rendering. This list is not updated when a row name changes in RowNames.
        /// </summary>
        public List<string> RowNamesUI { get; private set; }


        public int RowCount => RowNames?.Count ?? 0;

        public int ColumnCount => ColumnNames?.Count ?? 0;

        /// <summary>
        /// If this 2DA instance has been modified since it was loaded
        /// </summary>
        public bool IsModified
        {
            get
            {
                if (Cells == null) return false;
                for (int i = 0; i < RowCount; i++)
                {
                    for (int j = 0; j < ColumnCount; j++)
                    {
                        Bio2DACell c = Cells[i, j];
                        if (c == null) continue;
                        if (c.IsModified) return true;
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// Export that was used to load this Bio2DA. Is null if an export was not used to load this 2DA
        /// </summary>
        public ExportEntry Export;

        public Bio2DA(ExportEntry export)
        {
            //Console.WriteLine("Loading " + export.ObjectName);
            Export = export;

            _rowNames = new List<string>();
            RowNamesUI = new List<string>();
            if (export.ClassName == "Bio2DA")
            {
                const string rowLabelsVar = "m_sRowLabel";
                var props = export.GetProperty<ArrayProperty<NameProperty>>(rowLabelsVar);
                if (props != null)
                {
                    foreach (NameProperty n in props)
                    {
                        _rowNames.Add(n.Value.Instanced);
                        RowNamesUI.Add(n.Value.Instanced.Replace("_", "__"));
                    }
                }
                else
                {
                    Debug.WriteLine("Unable to find row names property!");
                    return;
                }
            }
            else
            {
                var props = export.GetProperty<ArrayProperty<IntProperty>>("m_lstRowNumbers");//Bio2DANumberedRows
                if (props != null)
                {
                    foreach (IntProperty n in props)
                    {
                        _rowNames.Add(n.Value.ToString());
                        RowNamesUI.Add(n.Value.ToString());
                    }
                }
                else
                {
                    Debug.WriteLine("Unable to find row names property (m_lstRowNumbers)!");
                    return;
                }
            }

            var binary = export.GetBinaryData<Bio2DABinary>();

            _columnNames = new List<string>(binary.ColumnNames.Select(n => n.Name));
            mappedColumnNames = new CaseInsensitiveDictionary<int>(ColumnNames.Count);
            for (int i = 0; i < ColumnCount; i++)
            {
                mappedColumnNames[ColumnNames[i]] = i;
            }

            mappedRowNames = new CaseInsensitiveDictionary<int>(RowCount);
            for (int i = 0; i < RowCount; i++)
            {
                mappedRowNames[RowNames[i]] = i;
            }

            Cells = new Bio2DACell[RowCount, ColumnCount];
            foreach ((int index, Bio2DABinary.Cell cell) in binary.Cells)
            {
                int row = index / ColumnCount;
                int col = index % ColumnCount;
                this[row, col] = cell.Type switch
                {
                    Bio2DABinary.DataType.INT => new Bio2DACell(cell.IntValue),
                    Bio2DABinary.DataType.NAME => new Bio2DACell(cell.NameValue, export.FileRef),
                    Bio2DABinary.DataType.FLOAT => new Bio2DACell(cell.FloatValue),
                    _ => throw new ArgumentOutOfRangeException()
                };
            }

            IsIndexed = binary.IsIndexed;
        }

        /// <summary>
        /// Initializes a blank Bio2DA. Cells is not initialized, the caller must set up this Bio2DA.
        /// </summary>
        public Bio2DA()
        {
            _columnNames = new List<string>();
            _rowNames = new List<string>();
            mappedRowNames = new CaseInsensitiveDictionary<int>();
            mappedColumnNames = new CaseInsensitiveDictionary<int>();
        }

        /// <summary>
        /// Marks all cells as not modified
        /// </summary>
        internal void MarkAsUnmodified()
        {
            for (int rowindex = 0; rowindex < RowCount; rowindex++)
            {
                for (int colindex = 0; colindex < ColumnCount; colindex++)
                {
                    Bio2DACell cell = Cells[rowindex, colindex];
                    if (cell != null)
                    {
                        cell.IsModified = false;
                    }
                }
            }
        }

        public void Write2DAToExport(ExportEntry export = null)
        {
            var binary = new Bio2DABinary
            {
                ColumnNames = ColumnNames.Select(s => new NameReference(s)).ToList(),
                Cells = new OrderedMultiValueDictionary<int, Bio2DABinary.Cell>(),
                Export = Export,
                IsIndexed = IsIndexed
            };

            for (int rowindex = 0; rowindex < RowCount; rowindex++)
            {
                for (int colindex = 0; colindex < ColumnCount; colindex++)
                {
                    Bio2DACell cell = Cells[rowindex, colindex];
                    if (cell != null)
                    {
                        int index = (rowindex * ColumnCount) + colindex;
                        binary.Cells.Add(index, cell.Type switch
                        {
                            Bio2DACell.Bio2DADataType.TYPE_INT => new Bio2DABinary.Cell { IntValue = cell.IntValue, Type = Bio2DABinary.DataType.INT },
                            Bio2DACell.Bio2DADataType.TYPE_NAME => new Bio2DABinary.Cell { NameValue = cell.NameValue, Type = Bio2DABinary.DataType.NAME },
                            Bio2DACell.Bio2DADataType.TYPE_FLOAT => new Bio2DABinary.Cell { FloatValue = cell.FloatValue, Type = Bio2DABinary.DataType.FLOAT },
                            _ => throw new ArgumentOutOfRangeException()
                        });
                    }
                }
            }

            Property rowsProp = Export.ClassName switch
            {
                "Bio2DA" => new ArrayProperty<NameProperty>(RowNames.Select(n => new NameProperty(n)), "m_sRowLabel"),
                "Bio2DANumberedRows" => new ArrayProperty<IntProperty>(RowNames.Select(n => new IntProperty(int.Parse(n))), "m_lstRowNumbers"),
                _ => throw new ArgumentOutOfRangeException()
            };

            // This is so newly minted 2DA can be installed into an export.
            export ??= Export;
            export.WritePropertyAndBinary(rowsProp, binary);
        }

        internal string GetColumnNameByIndex(int columnIndex)
        {
            if (columnIndex < ColumnNames.Count && columnIndex >= 0)
            {
                return ColumnNames[columnIndex];
            }
            return null;
        }

        public int GetColumnIndexByName(string columnName)
        {
            return mappedColumnNames[columnName];
        }


        public int GetRowIndexByName(string rowname)
        {
            return mappedRowNames[rowname];
        }

        #region Setters / Accessors

        /// <summary>
        /// Maps column names to their indices for faster lookups
        /// </summary>
        private CaseInsensitiveDictionary<int> mappedColumnNames;

        /// <summary>
        /// Maps row names to their indices for faster lookups. For Bio2DA, lookups directly use the string value. For Bio2DANumberedRows, a .ToString() is run first on the lookup value so we don't have to maintain a different dictionary.
        /// </summary>
        private CaseInsensitiveDictionary<int> mappedRowNames;

        /// <summary>
        /// Adds a new row of the specified name to the table. If using Bio2DANumberedRows, pass a string version of an int. If a row already exists with this name, the index for that row is returned instead.
        /// </summary>
        /// <param name="rowName"></param>
        /// <returns></returns>
        public int AddRow(string rowName)
        {
            if (mappedRowNames.TryGetValue(rowName, out var existing))
            {
                return existing;
            }
         
            mappedRowNames[rowName] = _rowNames.Count; // 0 based
            _rowNames.Add(rowName);
            return _rowNames.Count - 1;
        }

        /// <summary>
        /// Accesses a 2DA by cell coordinates starting from the top left of 0,0.
        /// </summary>
        /// <param name="rowindex"></param>
        /// <param name="colindex"></param>
        /// <returns></returns>
        public Bio2DACell this[int rowindex, int colindex]
        {
            get => Cells[rowindex, colindex];
            set
            {
                // set the item for this index. value will be of type Bio2DACell.
                Cells[rowindex, colindex] = value;
            }
        }

        /// <summary>
        /// Accesses a 2DA by row name and the column index, starting from the left of 0. For Bio2DANumberedRows, you must pass the row name as a string version of the row number value.
        /// </summary>
        /// <param name="rowname">Row name (case insensitive)</param>
        /// <param name="colindex"></param>
        /// <returns></returns>
        public Bio2DACell this[string rowname, int colindex]
        {
            get
            {
                if (mappedRowNames.TryGetValue(rowname, out var rowIndex))
                {
                    return Cells[rowIndex, colindex];
                }

                throw new Exception($"A row named '{rowname}' was not found in the 2DA");
            }
            set
            {
                // set the item for this index. value will be of type Bio2DACell.
                if (mappedRowNames.TryGetValue(rowname, out var rowIndex))
                {
                    Cells[rowIndex, colindex] = value;
                }
                else
                {
                    throw new Exception($"A row named '{rowname}' was not found in the 2DA");
                }
            }
        }

        /// <summary>
        /// Accesses a 2DA by row index and a column name, starting from the top of 0.
        /// </summary>
        /// <param name="rowindex"></param>
        /// <param name="columnname">Column name (case insenitive)</param>
        /// <returns></returns>
        public Bio2DACell this[int rowindex, string columnname]
        {
            get
            {
                if (mappedColumnNames.TryGetValue(columnname, out var colindex))
                {
                    return Cells[rowindex, colindex];
                }

                throw new Exception($"A column named '{columnname}' was not found in the 2DA");
            }
            set
            {
                // set the item for this index. value will be of type Bio2DACell.
                if (mappedColumnNames.TryGetValue(columnname, out var colindex))
                {
                    Cells[rowindex, colindex] = value;
                }
                else
                {
                    throw new Exception($"A column named '{columnname}' was not found in the 2DA");
                }
            }
        }

        /// <summary>
        /// Accesses a 2DA by row name and a column name. For Bio2DANumberedRows, you must pass the row name as a string version of the row number value.
        /// </summary>
        /// <param name="rowname">Row name (case insensitive). For Bio2DANumberedRows, pass the value of the row name as a string.</param>
        /// <param name="columnname">Column name (case insenitive)</param>
        /// <returns></returns>
        public Bio2DACell this[string rowname, string columnname]
        {
            get
            {
                if (mappedColumnNames.TryGetValue(columnname, out var colindex))
                {
                    if (mappedRowNames.TryGetValue(rowname, out var rowindex))
                    {
                        return Cells[rowindex, colindex];
                    }
                    throw new Exception($"A row named '{rowname}' was not found in the 2DA");
                }
                throw new Exception($"A column named '{columnname}' was not found in the 2DA");
            }
            set
            {
                // set the item for this index. value will be of type Bio2DACell.
                if (mappedColumnNames.TryGetValue(columnname, out var colindex))
                {
                    if (mappedRowNames.TryGetValue(rowname, out var rowindex))
                    {
                        Cells[rowindex, colindex] = value;
                    }
                    else
                    {
                        throw new Exception($"A row named '{rowname}' was not found in the 2DA");
                    }
                }
                else
                {
                    throw new Exception($"A column named '{columnname}' was not found in the 2DA");
                }
            }
        }

        #endregion
    }
}
