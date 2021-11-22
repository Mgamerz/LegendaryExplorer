using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Packages;
using PropertyChanged;

namespace LegendaryExplorerCore.TLK.ME1
{
    public class ME1TalkFile : IEquatable<ME1TalkFile>, ITalkFile
    {
        #region structs
        public struct HuffmanNode
        {
            public int LeftNodeID;
            public int RightNodeID;
            public char data;

            public HuffmanNode(int r, int l)
                : this()
            {
                RightNodeID = r;
                LeftNodeID = l;
            }

            public HuffmanNode(char c)
                : this()
            {
                data = c;
                LeftNodeID = -1;
                RightNodeID = -1;
            }
        }

        #endregion

        public MELocalization Localization { get; set; }
        public List<TLKStringRef> StringRefs { get; set; }


        private List<HuffmanNode> nodes;

        private Dictionary<int, string> StringRefsTable;

        public int UIndex;

        public string language;
        public bool male;
        public readonly string FilePath;

        public string Name;
        public string BioTlkSetName;

        /// <summary>
        /// If TLK is modified. This should not be trusted as you can directly edit StringRefs. Only use if your own code sets it.
        /// </summary>
        public bool IsModified { get; set; }

        #region Constructors
        public ME1TalkFile(IMEPackage pcc, int uIndex) : this(pcc, pcc.GetUExport(uIndex))
        {
        }

        public ME1TalkFile(ExportEntry export) : this(export.FileRef, export)
        {
        }

        private ME1TalkFile(IMEPackage pcc, ExportEntry export)
        {
            if (!pcc.Game.IsGame1())
            {
                throw new Exception("ME1 Unreal TalkFile cannot be initialized with a non-ME1 file");
            }
            UIndex = export.UIndex;
            LoadTlkData(pcc);
            FilePath = pcc.FilePath;
            Name = export.ObjectName.Instanced;
            BioTlkSetName = export.ParentName; //Not technically the tlkset name, but should be about the same
            Localization = pcc.Localization;
        }

        /// <summary>
        /// Amount of strings in this Talk file
        /// </summary>
        public int StringRefCount => StringRefsTable.Count;
        
 
        #endregion

        //ITalkFile

        public string FindDataById(int strRefID, bool withFileName = false, bool returnNullIfNotFound = false, bool noQuotes = false, bool male = true)
        {
            string data;
            if (StringRefsTable.TryGetValue(strRefID, out data))
            {
                if (noQuotes)
                    return data;

                var retdata = "\"" + data + "\"";
                if (withFileName && FilePath != null)
                {
                    retdata += " (" + Path.GetFileName(FilePath) + ")";
                }
                return retdata;
            }

            return returnNullIfNotFound ? null : "No Data";
        }

        /// <summary>
        /// Find the matching string id for the specified string. Returns -1 if not found. The male parameter is not used.
        /// </summary>
        /// <param name="tf"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public int FindIdByData(string value, bool male = true)
        {
            // Male is not used
            var matching = StringRefs.FirstOrDefault(x => x.Data == value);
            if (matching != null) return matching.StringID;
            return -1;
        }


        #region IEquatable
        public bool Equals(ME1TalkFile other)
        {
            return (other?.UIndex == UIndex && other.FilePath == FilePath);
        }

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }
            return Equals(obj as ME1TalkFile);
        }

        public override int GetHashCode()
        {
            return 1;
        }
        #endregion

        #region Load Data
        private void LoadTlkData(IMEPackage pcc)
        {
            var r = new EndianReader(pcc.GetUExport(UIndex).GetReadOnlyBinaryStream(), Encoding.Unicode)
            {
                Endian = pcc.Endian
            };
            //hashtable
            int entryCount = r.ReadInt32();
            StringRefsTable = new Dictionary<int, string>(entryCount);
            List<TLKStringRef> stringRefs = new List<TLKStringRef>(entryCount);
            for (int i = 0; i < entryCount; i++)
            {
                stringRefs.Add(new TLKStringRef(r, true));
            }

            //Huffman tree
            int nodeCount = r.ReadInt32();
            nodes = new List<HuffmanNode>(nodeCount);
            for (int i = 0; i < nodeCount; i++)
            {
                bool leaf = r.ReadBoolean();
                if (leaf)
                {
                    nodes.Add(new HuffmanNode(r.ReadChar()));
                }
                else
                {
                    nodes.Add(new HuffmanNode(r.ReadInt16(), r.ReadInt16()));
                }
            }
            //TraverseHuffmanTree(nodes[0], new List<bool>());

            //encoded data
            int stringCount = r.ReadInt32();
            byte[] data = new byte[r.BaseStream.Length - r.BaseStream.Position];
            r.Read(data, 0, data.Length);
            var bits = new BitArray(data);

            //decompress encoded data with huffman tree
            int offset = 4;
            var rawStrings = new List<string>(stringCount);
            while (offset * 8 < bits.Length)
            {
                int size = BitConverter.ToInt32(data, offset);
                offset += 4;
                string s = GetString(offset * 8, bits);
                offset += size + 4;
                rawStrings.Add(s);
            }

            //associate StringIDs with strings
            foreach (TLKStringRef strRef in stringRefs)
            {
                StringRefsTable[strRef.Index] = strRef.Flags == 1 ? rawStrings[strRef.Index] : null;
                /*
                if (strRef.Flags == 1)
                {
                    strRef.Data = rawStrings[strRef.Index];
                }*/
            }
        }

        private string GetString(int bitOffset, BitArray bits)
        {
            HuffmanNode root = nodes[0];
            HuffmanNode curNode = root;

            var builder = new StringBuilder();
            int i;
            for (i = bitOffset; i < bits.Length; i++)
            {
                /* reading bits' sequence and decoding it to Strings while traversing Huffman Tree */
                int nextNodeID = bits[i] ? curNode.RightNodeID : curNode.LeftNodeID;

                /* it's an internal node - keep looking for a leaf */
                if (nextNodeID >= 0)
                    curNode = nodes[nextNodeID];
                else
                /* it's a leaf! */
                {
                    char c = curNode.data;
                    if (c != '\0')
                    {
                        /* it's not NULL */
                        builder.Append(c);
                        curNode = root;
                        i--;
                    }
                    else
                    {
                        /* it's a NULL terminating processed string, we're done */
                        //skip ahead approximately 9 bytes to the next string
                        return builder.ToString();
                    }
                }
            }

            if (curNode.LeftNodeID == curNode.RightNodeID)
            {
                char c = curNode.data;
                //We hit edge case where final bit is on a byte boundary and there is nothing left to read. This is a leaf node.
                if (c != '\0')
                {
                    /* it's not NULL */
                    builder.Append(c);
                    curNode = root;
                }
                else
                {
                    /* it's a NULL terminating processed string, we're done */
                    //skip ahead approximately 9 bytes to the next string
                    return builder.ToString();
                }
            }

            Debug.WriteLine("RETURNING NULL STRING (NOT NULL TERMINATED)!");
            return null;
        }

        private void TraverseHuffmanTree(HuffmanNode node, List<bool> code)
        {
            /* check if both sons are null */
            if (node.LeftNodeID == node.RightNodeID)
            {
                BitArray ba = new BitArray(code.ToArray());
                string c = "";
                foreach (bool b in ba)
                {
                    c += b ? '1' : '0';
                }
            }
            else
            {
                /* adds 0 to the code - process left son*/
                code.Add(false);
                TraverseHuffmanTree(nodes[node.LeftNodeID], code);
                code.RemoveAt(code.Count - 1);

                /* adds 1 to the code - process right son*/
                code.Add(true);
                TraverseHuffmanTree(nodes[node.RightNodeID], code);
                code.RemoveAt(code.Count - 1);
            }
        }
        #endregion


        /// <summary>
        /// Saves this TLK object to XML
        /// </summary>
        /// <param name="fileName"></param>
        public void SaveToXML(string fileName)
        {
            using var xr = new XmlTextWriter(fileName, Encoding.UTF8);
            WriteXML(StringRefsTable, Name, xr);
        }

        /// <summary>
        /// Replaces a string in the list of StringRefs.
        /// </summary>
        /// <param name="stringID">The ID of the string to replace.</param>
        /// <param name="newText">The new text of the string.</param>
        /// <param name="addIfNotFound">If the string should be added as new stringref if it is not found. Default is false.</param>
        /// <returns>True if the string was found, false otherwise.</returns>
        public bool ReplaceString(int stringID, string newText, bool addIfNotFound = false)
        {
            if (StringRefsTable.ContainsKey(stringID))
            {
                IsModified = true;
                StringRefsTable[stringID] = newText;
            }
            else if (addIfNotFound)
            {
                IsModified = true;
                AddString(new TLKStringRef(stringID, 0, newText));
                return false; // Was not found, but was added.
            }
            else
            {
                // Not found, not added
                return false;
            }

            return true;
        }

        /// <summary>
        /// Adds a new string reference to the TLK. Marks the TLK as modified.
        /// </summary>
        /// <param name="sref"></param>
        public void AddString(TLKStringRef sref)
        {
            StringRefsTable[sref.StringID] = sref.Data;
            IsModified = true;
        }

        private static void WriteXML(Dictionary<int, string> stringRefTable, string name, XmlTextWriter writer)
        {

            writer.Formatting = Formatting.Indented;
            writer.Indentation = 4;

            writer.WriteStartDocument();
            writer.WriteStartElement("tlkFile");
            writer.WriteAttributeString("Name", name);

            foreach (var tlkStringRef in stringRefTable.OrderBy(x=>x.Key))
            {
                var flag = tlkStringRef.Value == null ? 0 : 1;
                writer.WriteStartElement("string");
                writer.WriteStartElement("id");
                writer.WriteValue(tlkStringRef.Key);
                writer.WriteEndElement(); // </id>
                writer.WriteStartElement("flags");
                writer.WriteValue(flag);
                writer.WriteEndElement(); // </flags>
                if (flag != 1)
                    writer.WriteElementString("data", "-1");
                else
                    writer.WriteElementString("data", tlkStringRef.Value);
                writer.WriteEndElement(); // </string>
            }
            writer.WriteEndElement(); // </tlkFile>
        }

        public static string TLKtoXmlstring(string name, Dictionary<int, string> tlkStringRefTable)
        {
            var InputTLK = new StringBuilder();
            using var stringWriter = new StringWriter(InputTLK);
            using var writer = new XmlTextWriter(stringWriter);
            WriteXML(tlkStringRefTable, name, writer);
            return InputTLK.ToString();
        }


    }
}