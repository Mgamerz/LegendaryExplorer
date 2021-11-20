using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PropertyChanged;

namespace LegendaryExplorerCore.TLK
{
    /// <summary>
    /// Representation of a string in a TLK file.
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    [DebuggerDisplay("TLKStringRef {StringID} {Data}")]
    public class TLKStringRef : IEquatable<TLKStringRef>, IComparable
    {
        public int StringID { get; set; }
        public string Data { get; set; }
        public int Flags { get; set; }
        public int Index { get; set; }

        public int BitOffset
        {
            get => Flags;
            //use same variable to save memory as flags is not used in me2/3, but bitoffset is.
            set => Flags = value;
        }

        public int CalculatedID => StringID >= 0 ? StringID : -(int.MinValue - StringID);

        /// <summary>
        /// This is used by huffman compression
        /// </summary>
        public string ASCIIData
        {
            get
            {
                if (Data == null)
                {
                    return "-1\0";
                }
                if (Data.EndsWith("\0", StringComparison.Ordinal))
                {
                    return Data;
                }
                return Data + '\0';
            }
        }

        public TLKStringRef(BinaryReader r, bool me1)
        {
            StringID = r.ReadInt32();
            if (me1)
            {
                Flags = r.ReadInt32();
                Index = r.ReadInt32();
            }
            else
            {
                BitOffset = r.ReadInt32();
            }
        }

        public TLKStringRef(int id, int flags, string data, int index = -1)
        {
            StringID = id;
            Flags = flags;
            Data = data;
            Index = index;
        }

        public bool Equals(TLKStringRef other)
        {
            return StringID == other.StringID && ASCIIData == other.ASCIIData && Flags == other.Flags /*&& Index == other.Index*/;
        }
        public int CompareTo(object obj)
        {
            TLKStringRef entry = (TLKStringRef)obj;
            return Index.CompareTo(entry.Index);

        }
    }
}
