/*


MIT License

Copyright (c) 2019 Git-SiN

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.




"
    This Class is for parsing the txt files that show the Structures of KERNEL OBJECTs.
    These Files are copies from 'winDbg.exe' using the 'dt' Instruction with '-r' option (or not).
        - The Name of these files must start with "MR_".

        ex] ntdll!_HANDLE_TRACE_DB_ENTRY                // HAVE TO INCLUDE THIS LINE.
                +0x000 ClientId         : _CLIENT_ID
                  +0x000 UniqueProcess    : Ptr32 Void
                  +0x004 UniqueThread     : Ptr32 Void
               +0x008 Handle           : Ptr32 Void
               +0x00c Type             : Uint4B
               +0x010 StackTrace       : [16] Ptr32 Void


    Parsed structures will be used to parse the real data stored at the specific addresses
    located in kernel space of each Process.  
    
    It needs more Routines;
        1. Interface-related Routines.
            - Add, Remove, Replace and so on.
        2. Routines for receiving the Real Data that was copied from the memory.
        3. Interface for the output of the parsed data.



"


*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Threading;

namespace UIforMR
{
    class KernelObjects
    {
        public class KOBJECT_FIELD
        {
            public string Name { get; }
            public string Description { get; }
            public uint Offset { get; }

            /*                                                                                                                                              (Max Value)
            00		        0		    0 		    0		    000 			        00		        00 0000 0000		0000 00    	    00 0000
            UNION           GRANULARITY	ARRAY		POINTER     DATA TYPE		        OUTPUT TYPE	    Resv.               Value1          Value2
            ============================================================================================================================================================
            10 : Union	    1 : BIT		-		    -		    -			            		        -                   Position(64)	Bits(64)
            11 : Union-Start0 : BYTE	1 : ARRAY                                       <------- Array Count(4,096) ------->
                        		                    1 : POINTER 011 : 64-bits Address

								                                111 : Another OBJECT                                        <----------- OBJECT SIZE(4,096) ----------->		
								                                110 : LIST_ENTRY                        ->                                  00 0001 : SINGLE_LIST_ENTRY			
								                                101 : LARGE_INTEGER                     ->                                  01 0000 : ULARGE_INTEGER                                                        
								                                100 : UNICODE_STRING                    ->                                  00 0001 : [ANSI_]STRING

								                                010 : <unnamed-tag>     10 : ObjectType <------------------------ unnamed SIZE ------------------------>
                                                                001 : PADDING FIELD     <-------------------------------- PADDING SIZE -------------------------------->
								                                000 : Default DATA TYPE					                                    00 ____ :   signed_
															                                                                                01 ____ : unsigned_
															                            00 : HEXA                                           __ 0001 :           _CHAR
                                                                                        01 : BINARY                                         __ 0010 :	        _INT2B
											                                            10 : DECIMAL			                            __ 0100 : 	        _INT4B
											                                            11 : DATE			                                __ 1000 : 	        _INT8B
                                                                                        
                                                                                                                                            10 0000	: Void
                                                                                                                                            11 0000	: void*
            */
            private uint fieldType;
            internal uint FieldType { get { return fieldType; } }

            internal KOBJECT_FIELD(uint offset, string name, string description)
            {
                Offset = offset;
                Name = name.Trim();
                Description = description.Trim();

                if (InitializeFieldType())
                {
                    throw (new Exception("UNKNOWN_SYMBOLS_FOUND")); 
                }
            }

            /// <summary>
            /// It makes a PADDING FIELD.
            /// </summary>
            /// <param name="offset"></param>
            /// <param name="size"></param>
            /// <param name="isInUnion"></param>
            internal KOBJECT_FIELD(uint offset, uint size, bool isInUnion)
            {
                if (size < 0x1000000)
                {
                    Offset = offset;
                    Name = "P-A-D-D-I-N-G   F-I-E-L-D";
                    Description = "P-A-D-D-I-N-G   B-Y-T-E-S";

                    this.fieldType = size;
                    this.fieldType |= (1 << 24);
                    if (isInUnion)
                        this.fieldType |= ((uint)1 << 31);
                }
                else
                    throw (new Exception("OVERSIZED_PADDING_BYTES"));   // It may not be occured..
            }

            private bool InitializeFieldType()
            {
                uint result = 0;
                bool hasUnknownSymbols = false;
    
                string[] keys = this.Description.ToLower().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (keys.Length > 0)
                {
                    if ((keys.First() == "pos") && (keys.Last().Contains("bit")) && (keys.Count() == 4))
                    {
                        result = 0x20400000;    // BIT | OUTPUT TYPE : BINARY
                        result |= ((Convert.ToUInt32(keys[1].Remove(keys[1].Length - 1))) << 6);
                        result |= (Convert.ToUInt32(keys[2]));
                    }
                    else if (keys.First() == "<unnamed-tag>")
                        result |= (1 << 25);
                    else
                    {
                        foreach (string key in keys)
                        {
                            if (key.First() == '_')
                            {
                                switch (key.Remove(0, 1))
                                {
                                    case "list_entry":
                                        result |= (6 << 24);
                                        break;
                                    case "single_list_entry":
                                        result |= (6 << 24);
                                        result |= 1;
                                        break;
                                    case "large_integer":
                                        result |= (5 << 24);
                                        break;
                                    case "ularge_integer":
                                        result |= (5 << 24);
                                        result |= 0x10;
                                        break;
                                    case "unicode_string":
                                        result |= (4 << 24);
                                        break;
                                    case "ansi_string":
                                        result |= (4 << 24);
                                        result |= 1;
                                        break;
                                    default:
                                        result |= (7 << 24);
                                        break;
                                }
                            }
                            else if (key.StartsWith("ptr"))
                            {
                                result |= (8 << 24);

                                if (key.EndsWith("64"))
                                    result |= (3 << 24);
                                else
                                {
                                    if (!key.EndsWith("32"))
                                    {
                                        hasUnknownSymbols = true;
                                    }

                                }
                            }
                            else if (key.Contains("int") && (key.Last() == 'b'))
                            {
                                if (key.First() == 'u')
                                    result |= 0x10;

                                if (key.Contains('2'))
                                    result |= 0x02;
                                else if (key.Contains('4'))
                                    result |= 0x04;
                                else if (key.Contains('8'))
                                    result |= 0x08;
                            }
                            else if (key.Contains("char"))
                            {
                                if (key.First() == 'u')
                                    result |= 0x10;

                                result |= 0x01;
                            }
                            else if ((key.First() == '[') && (key.Last() == ']'))
                            {
                                uint tmp = Convert.ToUInt32(key.Trim(new char[] { '[', ']' }));

                                // If the Array Count can't be marked by 12-Bits, Mark 0xFFF[MAX].
                                if (tmp > 0xFFF)
                                    tmp = 0xFFF;
                                result |= (tmp << 12);

                                result |= (1 << 28);
                            }
                            else if (key == "void")
                                result |= 0x20;
                            else if (key == "void*")
                                result |= 0x30;
                            else
                                hasUnknownSymbols = true;
                        }
                    }
                }

                this.fieldType = result;
                return hasUnknownSymbols;
            }

            /// <summary>
            /// </summary>
            /// <param name="flagsForUnion">
            /// 7-bit : UNION
            /// 6-bit : UNION-START
            /// 3-bit : REPLACE the UNION Flags.
            /// 2-bit : Update the marked Size of the 'Another OBJECT' Type. [Non-Marking the UNION flags.][It is only used alone.]
            /// 1-bit : This CALL is just for retrieving the Field Size. [Non-Marking the UNION flags.][It is only used alone.]
            /// 0-bit : REMOVED.
            /// </param>
            /// <param name="fieldSize">
            /// Next Field's Offset - Current Field's Offset
            /// </param>
            /// <returns>
            /// 0 : Failed. 
            /// Others : Calculated Field Size[If the Field is array, The return value is multiplied by the Array Count].
            /// 0xFFFFFFFF : The Size is UNKNOWN.
            /// </returns>
            internal uint IntermediateProcessing(byte flagsForUnion, uint fieldSize)
            {
                // bool isLastField = (flagsForUnion & 1) == 0? false: true;     // This Flag is REMOVED.
                bool isArray = ((this.fieldType >> 28) & 1) == 1 ? true : false;
                uint calculatedSize = 0;

                // Marking the UNION Flags.
                if((flagsForUnion & 6) == 0)
                {
                    this.fieldType &= (~(0xC0 << 24));  // Always, INITIALIZE.
                    this.fieldType |= (((uint)(flagsForUnion & 0xC0)) << 24);
                }

                // Just for Replacement.
                if ((flagsForUnion & 8) == 8)
                    return 0;   // Not Used.
                

                //////////////////////////////////////////////////////////////////////////////
                ////////////////               Calculate Size                 ////////////////
                //////////////////////////////////////////////////////////////////////////////
                if((this.fieldType & ((uint)0x2 << 28)) != 0)
                {
                    // 0. BIT :
                    // calculatedSize = 0;
                }
                else if ((this.fieldType & (8 << 24)) != 0)
                {
                    // 1. POINTER :                                    
                    calculatedSize = 4;
                    
                    // Ptr64
                    if (((this.fieldType >> 24) & 7) == 3)
                        calculatedSize  = calculatedSize << 1;
                }
                else
                {
                    // 2. DATA TYPE :
                    switch ((this.fieldType >> 24) & 7)
                    {
                        case 0:
                            // Default Data Type
                            calculatedSize = this.fieldType & 0xF;
                            break;
                        case 1:
                            // PADDING FIELD
                            return (this.fieldType & 0xFFFFFF);
                        case 2:
                            // <unnamed-tag>
                            if (flagsForUnion == 2)
                            {
                                calculatedSize = this.fieldType & 0x3FFFFF;

                                // 0 : is NOT Calculated yet. [UNKNOWN]
                                // 0x3FFFFF : The Size is in the UnnamedObjects List.
                                if ((calculatedSize == 0)/* || (calculatedSize == 0x3FFFFF)*/)
                                    return 0xFFFFFFFF;
                            }
                            else
                            {
                                // Mark the Size.
                                if (fieldSize != 0)
                                {
                                    // Always, Update.
                                    if (fieldSize < 0x400000)
                                    {
                                        this.fieldType &= ((uint)0x1FF << 23);
                                        this.fieldType |= fieldSize;
                                    }
                                    else
                                        this.fieldType |= 0x3FFFFF;     // The Size is in the UnnamedObjects List.
                                                                        //  -> I think that it will never be used, So I didn't deal with it... 
                                    calculatedSize = fieldSize;
                                }
                            }
                            break;
                        case 4:
                            // UNICODE_STRING & ANSI_STRING
                            calculatedSize = 8;
                            break;
                        case 5:
                            // LARGE_INTEGER & ULARGE_INTEGER
                            calculatedSize = 8;
                            break;
                        case 6:
                            // LIST_ENTRY
                            calculatedSize = 8;
                            if ((this.FieldType & 1) == 1)     // SINGLE_LIST_ENTRY
                                calculatedSize = calculatedSize >> 1;
                            break;
                        case 7:
                            // Another OBJECT
                            if (flagsForUnion == 2)     // This Condition is only for the performance when this Flag is set.
                            {
                                calculatedSize = this.FieldType & 0xFFF;      
                                if ((calculatedSize != 0) && (calculatedSize != 0xFFF))     // 0 : is NOT Calculated yet, 0xFFF : The Size is in the Registered List.
                                    break;
                            }
                            else if(flagsForUnion == 4) // Remove the marked size.
                            {
                                this.fieldType &= (~(uint)0xFFF);
                            }

                            int index = IndexOfThisObject(Registered, this.Description.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Last());
                            if (fieldSize != 0)
                            {
                                calculatedSize = fieldSize;

                                // If this field is the ARRAY type, divide with Array Count.
                                if (isArray)
                                {
                                    uint arrayCount = (this.FieldType >> 12) & 0xFFF;

                                    // If the marked Array Count is 0xFFF[MAX], It will be parsed direct for each calculation.
                                    if (arrayCount == 0xFFF)
                                        arrayCount = Convert.ToUInt32(this.Description.Split(new char[] { '[', ']' }, StringSplitOptions.RemoveEmptyEntries).First());

                                    calculatedSize /= arrayCount;
                                }

                                if (index == -1)
                                {
                                    try
                                    {
                                        KERNEL_OBJECT newObject = new KERNEL_OBJECT(Registered, this.Description.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Last(), calculatedSize);
                                    }
                                    catch (Exception e) {
                                        if(e.Message == "ALREADY_EXIST")
                                            System.Windows.Forms.MessageBox.Show(this.Description.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Last() + " is already EXIST.", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                                        else
                                            System.Windows.Forms.MessageBox.Show("Failed to create an Instance of KERNEL_OBJECT for " + this.Description.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Last(), "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                                    }
                                }
                            }
                            else
                            {
                                if (index != -1)
                                {
                                    calculatedSize = Registered[index].Size;    // This Value can be Zero or LOCKED.

                                    if ((calculatedSize != 0xFFFFFFFF) && ((calculatedSize >> 31) == 1))
                                        calculatedSize &= (~((uint)1 << 31));
                                }
                            }

                            // Mark the size at 'this.fieldType'.
                            if((this.fieldType & 0xFFF) == 0)
                            {
                                if (calculatedSize < 0x1000)
                                    this.fieldType |= calculatedSize;
                                else
                                    this.fieldType |= 0xFFF;
                            }

                            // UNKNOWN.
                            if ((calculatedSize == 0) || (calculatedSize == 0xFFFFFFFF))
                                return 0xFFFFFFFF;
                            break;
                        default:
                            // NONE
                            break;
                    }
                }

                // 3. ARRAY :
                if (isArray)
                {
                    uint arrayCount = (this.FieldType >> 12) & 0xFFF;

                    // If the marked Array Count is 0xFFF[MAX], It will be parsed direct for each calculation.
                    if (arrayCount == 0xFFF)
                        arrayCount = Convert.ToUInt32(this.Description.Split(new char[] { '[', ']' }, StringSplitOptions.RemoveEmptyEntries).First());

                    calculatedSize *= arrayCount;
                }

                return calculatedSize;
            }

            /// <summary>
            /// If the 'OriginalData' is Zero, it returns the Positions of BITs of this Field.
            /// If the 'OriginalData' is non-Zero, it returns the Value that Bit-Masked 'OriginalData' using this Field's Bits.
            /// </summary>
            /// <param name="OriginalData"></param>
            /// <returns> 0 : This Field is not the BIT Type.</returns>
            internal ulong GetBitPositionOrValue(ulong OriginalData)
            {
                ulong result = 0;
                ushort pos = 0;
                ushort bits = 0;

                if ((this.fieldType & (1 << 29)) != 0)
                {
                    bits = (ushort)(this.fieldType & 0x3F);
                    pos = (ushort)((this.fieldType >> 6) & 0x3F);

                    while (bits-- > 0)
                        result |= ((ulong)1 << bits);

                    if (OriginalData == 0)
                        return (result << pos);
                    else
                        return ((OriginalData >> pos) & result);
                }
                else
                    return 0;
            }

            internal byte GetSizeOfBitField()
            {
                ulong fieldSize = 0;
                byte result = 0;

                fieldSize = this.GetBitPositionOrValue(0);
                while (fieldSize != 0)
                {
                    fieldSize = (fieldSize >> 8);
                    result++;
                }
                
                return result;
            }

            internal void SetUnnamedObjectField(uint FieldSize)
            {
                this.fieldType |= (1 << 23);    // Mark the UnnamedObject Flag.
                this.IntermediateProcessing(4, FieldSize);
            }

            public void Show()
            {
                System.Windows.Forms.MessageBox.Show(String.Format("+0x{0:X3} {1,-17}: {2}", Offset, Name, Description), "Field Info", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
            }

            public string ToString(uint maxLengthOfName, uint maxLengthOfDesc, ushort depth = 0)
            {
                string tmp = "";
                string size;
                string format;

                if (maxLengthOfDesc == 0)
                    maxLengthOfDesc = 45;

                if (maxLengthOfName == 0)
                    maxLengthOfName = 45;

                while((depth--) > 0)
                    tmp += " ";
                
                if ((this.FieldType & (0x8 << 28)) != 0)
                {
                    if ((this.fieldType & (0x4 << 28)) != 0)
                        tmp += "   *";
                    else
                        tmp += "   -";
                }
                else
                    tmp += "  +";

                tmp += String.Format("0x{0:X3} {1}", this.Offset, this.Name);
                size = (IntermediateProcessing(2, 0) == 0xFFFFFFFF) ? "  -  " : String.Format("0x{0:X3}", IntermediateProcessing(2, 0));
                format = "{0,-" + maxLengthOfName + "} : {1,-" + maxLengthOfDesc + "}\t({2})";
                return String.Format(format, tmp, this.Description, size);
            }
        }

        public class KERNEL_OBJECT
        {
            public string Name { get; }
            public uint Size { get; set; }
            public List<KOBJECT_FIELD> Fields { get; set; }
            public List<KERNEL_OBJECT> UnnamedObjects { get; set; }

            internal KERNEL_OBJECT(List<KERNEL_OBJECT> theList, string name, uint size = 0)
            {
                if (theList != null)
                {
                    Name = name.Trim();
                    Size = size;
                    Fields = null;
                    UnnamedObjects = null;

                    if (IndexOfThisObject(theList, name) == -1)
                        theList.Add(this);
                    else
                    {
                        throw (new Exception("ALREADY_EXIST"));
                    }
                }
                else
                    throw (new Exception("NULL_LIST"));
            }

            internal bool AddField(KOBJECT_FIELD field)
            {
                if (Fields != null)
                {
                    Fields.Add(field);
                    return true;
                }
                else
                    return false;
            }

            internal void EndOfObject(uint ObjectSize = 0)
            {
                byte flagsForUnion = 0;
                uint fieldSize = 0;
                int i, j = 0;
                uint minOffset, maxOffset;

                if (this.Fields != null)
                {
                    if (this.Fields.Count > 0)
                    {
                        // Except the Last Field.
                        for (i = 0; i < Fields.Count - 1; i++)
                        {
                            if (Fields[i].Offset < Fields[i + 1].Offset)
                            {
                                fieldSize = Fields[i + 1].Offset - Fields[i].Offset;
                                if ((flagsForUnion & 0x80) != 0)
                                {
                                    // Always, i > 0
                                    if (Fields[i - 1].Offset < Fields[i].Offset)
                                    {
                                        // BIT UNION Type.
                                        if ((this.Fields[i].FieldType & (1 << 29)) != 0)
                                        {
                                            if ((this.Fields[i - 1].FieldType & (1 << 29)) != 0)
                                                flagsForUnion = 0x80;   // GENERALIZATION 
                                            else
                                                flagsForUnion = 0xC0;
                                        }
                                        else
                                            flagsForUnion = 0; // End of this UNION. 
                                    }
                                    else
                                        flagsForUnion = 0x80;
                                }
                                else
                                {
                                    if ((this.Fields[i].FieldType & (1 << 29)) != 0)    // BIT UNION Type.
                                        flagsForUnion = 0xC0;
                                }

                                Fields[i].IntermediateProcessing(flagsForUnion, fieldSize);
                            }
                            else if (Fields[i].Offset == Fields[i + 1].Offset)
                            {
                                if ((flagsForUnion & 0x80) == 0)
                                    flagsForUnion = 0xC0;
                                else
                                {
                                    // Always, i > 0
                                    if (Fields[i - 1].Offset >= Fields[i].Offset)
                                        flagsForUnion = 0x80;
                                    else
                                    {
                                        // BIT UNION Type.
                                        if ((this.Fields[i].FieldType & (1 << 29)) != 0)
                                        {
                                            if ((this.Fields[i - 1].FieldType & (1 << 29)) != 0)
                                                flagsForUnion = 0x80;   // GENERALIZATION 
                                            else
                                                flagsForUnion = 0xC0;
                                        }
                                        else
                                            flagsForUnion = 0xC0;
                                    }
                                }

                                Fields[i].IntermediateProcessing(flagsForUnion, 0);
                            }
                            else
                            {
                                //////////////////////////////////////////////////////////////////////////////
                                //////////////////          REPLACE the UNION Flags.        //////////////////
                                //////////////////////////////////////////////////////////////////////////////
                                minOffset = Fields[i + 1].Offset;
                                maxOffset = Fields[i].Offset;

                                // Search for the Minimum Offset of this UNION. 
                                for (j = 0; j < i; j++)
                                {
                                    if (Fields[j].Offset >= minOffset)
                                        break;
                                }

                                // Replace.
                                if ((Fields[j].FieldType >> 31) == 0)
                                    Fields[j].IntermediateProcessing(0xC8, 0);

                                while (++j < i)
                                {
                                    if ((Fields[j].FieldType >> 30) != 0x2)
                                        Fields[j].IntermediateProcessing(0x88, 0);
                                }

                                // NOW : j == i     -> From now on, It is the First Call & Every Field is in this UNION.
                                flagsForUnion = 0x80;

                                while (i < Fields.Count - 1)
                                {
                                    if (Fields[i].Offset < Fields[i + 1].Offset)
                                        fieldSize = Fields[i + 1].Offset - Fields[i].Offset;
                                    else
                                        fieldSize = 0;

                                    Fields[i].IntermediateProcessing(flagsForUnion, fieldSize);

                                    if (Fields[i + 1].Offset > maxOffset)
                                        break;
                                    else
                                        i++;
                                }

                                // The Last Field of this OBJECT. It is in this UNION.
                                if (i == Fields.Count - 1)
                                {
                                    LastProcessing(Fields[i].IntermediateProcessing(flagsForUnion, (ObjectSize > Fields[i].Offset) ? (ObjectSize - Fields[i].Offset) : 0));
                                    return;
                                }
                            }
                        }

                        // The Last Field of this OBJECT.
                        if (i > 0)
                        {
                            if (Fields[i - 1].Offset >= Fields[i].Offset)
                                flagsForUnion = 0x80;
                            else
                            {
                                // BIT UNION Type.
                                if ((this.Fields[i].FieldType & (1 << 29)) != 0)
                                {
                                    if ((flagsForUnion & 0x80) != 0)
                                        flagsForUnion = 0x80;
                                    else
                                    {
                                        if ((this.Fields[i - 1].FieldType & (1 << 29)) != 0)
                                            flagsForUnion = 0x80;
                                        else
                                            flagsForUnion = 0xC0;
                                    }
                                }
                                else
                                    flagsForUnion = 0;
                            }
                        }

                        LastProcessing(Fields[i].IntermediateProcessing(flagsForUnion, (ObjectSize > Fields[i].Offset) ? (ObjectSize - Fields[i].Offset) : 0));
                    }
                    else
                    {
                        // If the 'Fields.Count' is Zero, Delete 'Fields'.
                        this.Fields = null;
                    }
                }
                return;
            }

            internal void LastProcessing(uint LastFieldSize)
            {
                List<KOBJECT_FIELD> paddingFields = new List<KOBJECT_FIELD>();
                List<int> paddingIndex = new List<int>();
                List<int> unknownFields = new List<int>();
                uint fieldSize = 0;
                uint maximumOffsetInThisUnion = 0;
                byte dataType = 0;
                int i = 0;


                ///////////////////////////////////////////////////////////////////////////////////////////
                ////////////////////                  OBJECT SIZE                    //////////////////////
                ///////////////////////////////////////////////////////////////////////////////////////////
                // UNLOCK the OBJECT that the size has already been known.
                if((this.Size != 0xFFFFFFFF) && ((this.Size >> 31) == 1))
                    this.Size &= (~((uint)1 << 31));

                if ((LastFieldSize != 0) && (LastFieldSize != 0xFFFFFFFF))
                {
                    fieldSize = this.Fields.Last().Offset + LastFieldSize;
                    if (this.Size != fieldSize) {
                        if (!this.OverwriteTheObjectSize(fieldSize))
                        {
                            //return;
                        }
                    }       
                }


                ////////////////////////////////////////////////////////////////////////////////////////////
                ////////////////////                  UNKNOWN FIELDS                     ///////////////////
                ////////////////////////////////////////////////////////////////////////////////////////////
                LastFieldSize = 0;      // Reuse this Variable for calculating the Size of UNKNOWNSIZE OBJECTs.
                paddingFields.Clear();
                paddingIndex.Clear();

                for (i = 0; i < this.Fields.Count; i++)  // Include the Last Field.
                {
                    dataType = (byte)(this.Fields[i].FieldType >> 24);
                    if ((dataType & 0xC0) == 0xC0)
                    {
                        do
                        {
                            fieldSize = this.Fields[i].IntermediateProcessing(2, 0);
                            if (fieldSize == 0xFFFFFFFF)
                                unknownFields.Add(i);
                            else
                            {
                                if(fieldSize != 0)
                                {
                                    // OVERSIZED Unnamed Object.    -> NOT HANDLED...
                                    //if ((fieldSize == 0x3FFFFF) && ((dataType & 2) == 2))
                                    //{
                                    //}

                                    fieldSize += this.Fields[i].Offset;
                                    ///////////////////////////////////////////////////////////////////////////////////
                                    ///////////////     PADDING BYTES in UNIONs are not indicated.      ///////////////
                                    ///////////////////////////////////////////////////////////////////////////////////
                                    //try
                                    //{
                                    //    if (i < this.Fields.Count - 1)
                                    //    {
                                    //        if (fieldSize < this.Fields[i + 1].Offset)
                                    //        {
                                    //              Have to Insert direct.
                                    //        }
                                    //    }
                                    //    else
                                    //    {
                                    //        // It is the Last Field.
                                    //        if ((this.Size != 0xFFFFFFFF) && (fieldSize < this.Size))
                                    //        {
                                    //              Have to Insert direct.
                                    //        }
                                    //    }
                                    //}
                                    //catch (Exception e)
                                    //{
                                    //    if (e.Message == "OVERSIZED_PADDING_BYTES")
                                    //        continue;
                                    //    else
                                    //        throw e;
                                    //}
                                    ////////////////////////////////////////////////////////////////////////////////////
                                    ////////////////////////////////////////////////////////////////////////////////////
                                    ////////////////////////////////////////////////////////////////////////////////////
                                }
                                else
                                {
                                    // This Field is the BIT UNION Type.
                                    fieldSize = this.Fields[i].GetSizeOfBitField() + this.Fields[i].Offset;
                                }

                                if (maximumOffsetInThisUnion < fieldSize)
                                    maximumOffsetInThisUnion = fieldSize;
                            }

                            if ((++i) < this.Fields.Count) // GO to the NEXT Index that includes the last field.
                            {
                                dataType = (byte)(this.Fields[i].FieldType >> 24);
                                if (((dataType & 0xC0) != 0x80) && (this.Fields[i].Offset < maximumOffsetInThisUnion))
                                {
                                    /*
                                        It's to solve this problem; 
                                            If the Instructions are serialized like this, They are not bound together.
                                                *0x288 SameThreadApcFlags                   :  Uint4B                     	
                                                -0x288 Spare                                :  Pos 0, 1 Bit
                                                ..               	
                                                -0x288 OwnsSessionWorkingSetExclusive       :  Pos 7, 1 Bit               	
                                                *0x289 OwnsSessionWorkingSetShared          :  Pos 0, 1 Bit               	
                                                -0x289 OwnsProcessAddressSpaceExclusive     :  Pos 1, 1 Bit               	
                                                ..
                                                -0x289 OwnsChangeControlAreaShared          :  Pos 7, 1 Bit               	
                                                *0x28A OwnsPagedPoolWorkingSetExclusive     :  Pos 0, 1 Bit               	
                                                -0x28A OwnsPagedPoolWorkingSetShared        :  Pos 1, 1 Bit               	
                                                ..
                                                +0x28B PriorityRegionActive                 :  UChar                      	
                                            &&
                                                *0x214 PostBlockList                        :  _LIST_ENTRY    (0x008)
                                                -0x214 ForwardLinkShadow                    :  Ptr32 Void     (0x004)
                                                +0x218 StartAddress                         :  Ptr32 Void     (0x004)
                                    */

                                    // Replace the UNION FLAGs.
                                    this.Fields[i].IntermediateProcessing(0x88, 0);
                                    dataType = (byte)(this.Fields[i].FieldType >> 24);
                                }
                            }
                            else
                                break;
                        } while ((dataType & 0xC0) == 0x80);


                        ////////////////////////////////////////////////////////////////////////////////////////
                        //////////////////      Process the UNKNOWNSIZE Fields in UNIONs.     //////////////////
                        ////////////////////////////////////////////////////////////////////////////////////////
                        if (unknownFields.Count > 0)
                        {
                            // For Test...
                            // List<string> dbgMessage = new List<string>();

                            foreach (int index in unknownFields)
                            {
                                //dbgMessage.Clear();
                                //dbgMessage.Add("====================================================");
                                //dbgMessage.Add(this.Fields[index].ToString(2, 2));

                                dataType = (byte)(this.Fields[index].FieldType >> 24);

                                // Maybe, This condition is always TRUE.
                                if(((dataType & 7) == 2) || ((dataType & 7) == 7))
                                {
                                    fieldSize = maximumOffsetInThisUnion - this.Fields[index].Offset;
                                    dataType &= 0xC0;
                                    if (index < this.Fields.Count - 1)     // Except the Last Field.    
                                    {
                                        // GENERALIZATION; 
                                        //  If the following field exists of which the offset is bigger than the UNKNOWNSIZE Field's in this UNION,
                                        //      The size of this UNKNOWNSIZE Field should be limited by that following field's Offset.
                                        for (int j = index + 1; j < i; j++)
                                        {
                                            if (this.Fields[j].Offset > this.Fields[index].Offset)
                                            {
                                                fieldSize = this.Fields[j].Offset - this.Fields[index].Offset;
                                                break;
                                            }
                                        }
                                    }

                                    // STORE the calculated size.
                                    this.Fields[index].IntermediateProcessing(dataType, fieldSize);

                                    //dbgMessage.Add(this.Fields[index].ToString(2, 2));
                                    //dbgMessage.Add("====================================================");                                    
                                    //DebuggingForm debuggingForm = new DebuggingForm(String.Format("IN LAST PROCESSING : {0} - 0x{1:X3}", this.Name, this.Size), dbgMessage.ToArray(), System.Windows.Forms.MessageBoxButtons.OK);
                                    //debuggingForm.ShowDialog();
                                }
                                else
                                {
                                    // For Test...
                                    //System.Windows.Forms.MessageBox.Show(this.Fields[i].ToString(0, 0), "IN LAST PROCESSING : This Message can't be seen.");
                                }

                            }

                            unknownFields.Clear();
                        }

                        // If The Last Field is in UNION & The Size of this OBJECT is UNKNOWN, The 'maximumOffsetInThisUnion' is that.
                        if (i == this.Fields.Count) 
                        {
                            if(this.Size != maximumOffsetInThisUnion)
                            {
                                LastFieldSize = maximumOffsetInThisUnion;
                                    
                                // For Test..
                                //System.Windows.Forms.MessageBox.Show(String.Format("IN LAST PROCESSING : {0}\'s Size is 0x{1:X} -> 0x{2:X}", this.Name, this.Size, maximumOffsetInThisUnion));
                            }
                        }
                        else
                        {
                            // Check for the PADDING BYTES.
                            if (maximumOffsetInThisUnion < this.Fields[i].Offset) // The 'i' is out of this UNION. 
                            {
                                paddingIndex.Add(i + paddingFields.Count);  // So, The order of these two Instructions have to change.
                                paddingFields.Add(new KOBJECT_FIELD(maximumOffsetInThisUnion, (this.Fields[i].Offset - maximumOffsetInThisUnion), false));

                                // For Test...
                                // System.Windows.Forms.MessageBox.Show(paddingFields.Last().ToString(2, 2), this.Name);
                            }
                        }

                        // INITIALIZE.
                        maximumOffsetInThisUnion = 0;
                        i--;    // Decrease 'i' because of the 'i++' Instruction at the 'FOR Statement'.
                    }   // End Of the UNION.
                    else
                    {
                        fieldSize = this.Fields[i].IntermediateProcessing(2, 0);
                        if (fieldSize == 0)
                        {
                            // This Field is the BIT UNION Type of whitch the count is 8.
                            //  -> This case will be processed at the IntermediateProcessing().

                            // For Test...
                            //System.Windows.Forms.MessageBox.Show(this.Fields[i].ToString(2, 2), "IN LAST PROCESSING : ZERO FIELD.");
                        }
                        else if(fieldSize == 0xFFFFFFFF)
                        {
                            // Couldn't be figured out, Eventually.

                            // For Test...
                            //System.Windows.Forms.MessageBox.Show(this.Fields[i].ToString(2, 2), "IN LAST PROCESSING : Can't be figured out, Eventually.");
                        }
                        else
                        {
                            // OVERSIZED Unnamed Object.    -> NOT HANDLED...
                            //if ((fieldSize == 0x3FFFFF) && ((dataType & 2) == 2))
                            //{
                            //}

                            fieldSize += this.Fields[i].Offset;

                            // PADDING FIELD.
                            try
                            {
                                if (i < this.Fields.Count - 1)
                                {
                                    if (fieldSize < this.Fields[i + 1].Offset)
                                    {
                                        paddingFields.Add(new KOBJECT_FIELD(fieldSize, (this.Fields[i + 1].Offset - fieldSize), false));
                                        paddingIndex.Add(i + paddingFields.Count);
                                    }
                                }
                                else
                                {
                                    // It is the Last Field.
                                    if ((this.Size == 0) || (this.Size == 0xFFFFFFFF))
                                        LastFieldSize = fieldSize;
                                    else
                                    {
                                        if (fieldSize < this.Size)
                                        {
                                            paddingFields.Add(new KOBJECT_FIELD(fieldSize, this.Size - fieldSize, false));
                                            paddingIndex.Add(i + paddingFields.Count);
                                        }
                                        else if (fieldSize > this.Size)
                                            LastFieldSize = fieldSize;
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                if (e.Message == "OVERSIZED_PADDING_BYTES")
                                    continue;
                                else
                                    throw e;
                            }
                        }
                    }    
                }

                // Insert the PADDING FIELDS.
                if(paddingFields.Count > 0)
                {
                    for(i = 0; i < paddingFields.Count; i++)
                        this.Fields.Insert(paddingIndex[i], paddingFields[i]);
                }

                // Update the Size of this OBJECT.
                if (LastFieldSize != 0)
                    OverwriteTheObjectSize(LastFieldSize);

                // UNLOCK the Object that couldn't be figured out its Size, Eventually.
                if (this.Size == 0xFFFFFFFF)
                {
                    // For Test...
                    //System.Windows.Forms.MessageBox.Show(this.Name, "Eventually, Failed to figure out the Size.");

                    this.Size = 0;
                }

                return;
            }

            private void CheckWholeFieldsInRegisteredObejcts(List<KERNEL_OBJECT> theList)
            {   
                if (theList != null)
                {
                    foreach (KERNEL_OBJECT currentObject in theList)
                    {
                        if ((currentObject != this) && (currentObject.Fields != null))
                        {
                            bool isUpdated = false;

                            // Recursive for UnnamedObjects.
                            if (currentObject.UnnamedObjects != null)
                                CheckWholeFieldsInRegisteredObejcts(currentObject.UnnamedObjects);
                                
                            foreach (KOBJECT_FIELD field in currentObject.Fields)
                            {
                                if (field.Description.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Last().Trim() == this.Name)
                                {
                                    field.IntermediateProcessing(4, 0);
                                    isUpdated = true;
                                }

                                // If the Field is 'Unnamed Object' Type, Check that the size has been updated.
                                if ((((field.FieldType >> 23) & 0xF) == 5) && (currentObject.UnnamedObjects != null)) {
                                    foreach(KERNEL_OBJECT unnamedObject in currentObject.UnnamedObjects)
                                    {
                                        if(unnamedObject.Name.Trim() == field.Name.Trim())
                                        {
                                            if(((unnamedObject.Size >> 31) == 0) && (field.IntermediateProcessing(2, 0) != unnamedObject.Size))
                                            {
                                                field.IntermediateProcessing(4, unnamedObject.Size);
                                                isUpdated = true;
                                            }
                                            break;
                                        }
                                    }
                                }
                            }

                            // If the target OBJECT is being Parsed now, Its EndOfObject() should not be run.
                            if (isUpdated && ((currentObject.Size >> 31) == 0))
                                currentObject.EndOfObject(currentObject.Size);
                        }
                    }
                }
            }

            /// <param name="currentObject"></param>
            /// <param name="MaxLengthOfName"></param>
            /// <param name="MaxLengthOfDesc"></param>
            /// <param name="depth">It's for the different indentation of each level.</param>
            private void MaxLengthOfFields(KERNEL_OBJECT currentObject, ref ushort MaxLengthOfName, ref ushort MaxLengthOfDesc, ushort depth)
            {
                if (currentObject.UnnamedObjects != null)
                {
                    foreach(KERNEL_OBJECT unnamedObject in currentObject.UnnamedObjects)
                    {
                        MaxLengthOfFields(unnamedObject, ref MaxLengthOfName, ref MaxLengthOfDesc, (ushort)(depth + 3));    // 3 : Indentation Count.
                    }   
                }   

                if(currentObject.Fields != null)
                {
                    foreach (KOBJECT_FIELD field in currentObject.Fields)
                    {
                        if ((field.Name.Length + depth) > MaxLengthOfName)
                            MaxLengthOfName = (ushort)(field.Name.Length + depth);

                        if (field.Description.Length > MaxLengthOfDesc)
                            MaxLengthOfDesc = (ushort)field.Description.Length;
                    }
                }
            }

            private void FieldsInfoMaker(KERNEL_OBJECT currentObject, List<string> fieldsInfo, ushort MaxLengthOfName, ushort MaxLengthOfDesc, ushort depth)
            {
                if(currentObject.Fields != null)
                {
                    foreach (KOBJECT_FIELD currentField in currentObject.Fields)
                    {
                        fieldsInfo.Add(currentField.ToString(MaxLengthOfName, MaxLengthOfDesc, depth));

                        // If the FIeld is the 'Unnamed Object' Type, Show them together. 
                        if (((currentField.FieldType >> 23) & 0xF) == 5)
                        {
                            if (currentObject.UnnamedObjects != null)
                            {
                                if ((IndexOfThisObject(currentObject.UnnamedObjects, currentField.Name)) != -1)
                                    FieldsInfoMaker(currentObject.UnnamedObjects[IndexOfThisObject(currentObject.UnnamedObjects, currentField.Name)], fieldsInfo, MaxLengthOfName, MaxLengthOfDesc, (ushort)((currentField.FieldType >> 31) + depth + 2));
                            }
                        }
                    }
                }
            }

            internal void ShowFieldsInfo()
            {
                ushort MaxLengthOfName = 0;
                ushort MaxLengthOfDesc = 0;

                List<string> fieldsInfo = new List<string>();
                fieldsInfo.Clear();

                if (this.Fields != null)
                {
                    MaxLengthOfFields(this, ref MaxLengthOfName, ref MaxLengthOfDesc, 0);
                    MaxLengthOfName += 10;

                    fieldsInfo.Add(this.Name);
                    FieldsInfoMaker(this, fieldsInfo, MaxLengthOfName, MaxLengthOfDesc, 0);

                    DebuggingForm debuggingForm = null;
                    if ((this.Size > 0) && (this.Size < 0xFFFFFFFF))
                        debuggingForm = new DebuggingForm(String.Format("OBJECT Info : {0} - 0x{1:X}({2})", this.Name, this.Size, this.Size), fieldsInfo.ToArray(), System.Windows.Forms.MessageBoxButtons.OK);
                    else
                        debuggingForm = new DebuggingForm(String.Format("OBJECT Info : {0} - UNKNOWN SIZE", this.Name), fieldsInfo.ToArray(), System.Windows.Forms.MessageBoxButtons.OK);

                    debuggingForm.ShowDialog();
                }
            }

            internal bool OverwriteTheObjectSize(uint calculatedSize)
            {
                // For Test...
                if (calculatedSize == 0xFFFFFFFF) { 
                    System.Windows.Forms.MessageBox.Show("IN OVERWRITE() : calculatedSize is 0xFFFFFFFF");
                    return false;
                }

                if ((this.Size == 0) || (this.Size == 0xFFFFFFFF))  // 0 : This OBJECT could not be figured out its size over the 3-Level Calculating.
                {
                    this.Size = calculatedSize;
                    CheckWholeFieldsInRegisteredObejcts(Registered);
                    return true;
                }

                if (SHOW_DEBUGGING_MESSAGE_FOR_OVERWRITTING_THE_OBJECT_SIZE)
                {
                    List<string> context = new List<string>();
                    ushort maxLengthOfName = 0;
                    ushort maxLengthOfDesc = 0;

                    context.Clear();

                    context.Add("====================================================");
                    context.Add(String.Format(" ::: {0} : 0x{1:X3} ({2})\t\t\t:::", "Registered Size", this.Size, this.Size));
                    context.Add(String.Format(" ::: {0} : 0x{1:X3} ({2})\t\t\t:::", "Calculated Size", calculatedSize, calculatedSize));
                    context.Add("====================================================\r\n");
                    context.Add(this.Name);

                    foreach (KOBJECT_FIELD field in this.Fields)
                    {
                        if (field.Name.Length > maxLengthOfName)
                            maxLengthOfName = (ushort)field.Name.Length;
                        if (field.Description.Length > maxLengthOfDesc)
                            maxLengthOfDesc = (ushort)field.Description.Length;
                    }
                    maxLengthOfName += 10;

                    for (int i = 0; i < this.Fields.Count; i++)
                        context.Add(this.Fields[i].ToString(maxLengthOfName, maxLengthOfDesc));

                    DebuggingForm debuggingForm = new DebuggingForm("Size Check for " + this.Name, context.ToArray(), System.Windows.Forms.MessageBoxButtons.YesNo, "Do you want to OVERWRITE it?");
                    if (debuggingForm.ShowDialog() == System.Windows.Forms.DialogResult.Yes)
                    {
                        this.Size = calculatedSize;
                        CheckWholeFieldsInRegisteredObejcts(Registered);
                        return true;
                    }
                    else
                        return false;
                }
                else
                {
                    // Always, OverWrite.
                    this.Size = calculatedSize;
                    CheckWholeFieldsInRegisteredObejcts(Registered);
                    return true;
                }
            }



            //////////////////////////////////////////////////////////////////////
            /////////////////////          INTERFACE         /////////////////////
            //////////////////////////////////////////////////////////////////////
            internal int GetFieldOffset(string FieldName)
            {
                if ((FieldName.Length > 0) && (this.Fields.Count > 0))
                {
                    foreach (KOBJECT_FIELD field in this.Fields)
                    {
                        if (field.Name == FieldName.Trim())
                            return (int)(field.Offset);
                    }
                }

                return -1;
            }
        }


        //////////////////////////////////////////////////////////////////////
        ////////////////////          START POINT         ////////////////////
        //////////////////////////////////////////////////////////////////////
        internal volatile static List<KERNEL_OBJECT> Registered = null;
        internal MainForm form = null;
        //private Thread ParsingThread = null;
        internal static System.Drawing.Point debuggingFormLocation = System.Drawing.Point.Empty;    // Store the Last Location of Debugging Form.
        

        // Flags.
        internal static bool SHOW_DEBUGGING_MESSAGE_FOR_OVERWRITTING_THE_OBJECT_SIZE = false;
        internal static bool isParsingNewFile = false;

        public KernelObjects(MainForm f)
        {
            form = f;

            Registered = new List<KERNEL_OBJECT>();
            Registered.Clear();

            InitializeList();
           
            // Initialize this class in the new THREAD.
            //ParsingThread = new Thread(InitializeList);
            //ParsingThread.Start();
        }

        /// <summary>
        /// If the object is not found, return -1
        /// </summary>
        /// <param name="ObjectName"></param>
        /// <returns></returns>
        public static int IndexOfThisObject(List<KERNEL_OBJECT> theList, string ObjectName)
        {
            if (theList != null)
            {
                if (theList.Count > 0)
                {
                    int i = 0;

                    for (i = 0; i < theList.Count; i++)
                    {
                        if (theList[i].Name.Trim() == ObjectName.Trim())
                            return i;
                    }
                }

                return -1;
            }
            else
                throw (new Exception("NULL_LIST"));
        }

        private static KERNEL_OBJECT RequestNewObject(List<KERNEL_OBJECT> theList, string ObjectName)
        {
            KERNEL_OBJECT requestedObject = null;

            try
            {
                requestedObject = new KERNEL_OBJECT(theList, ObjectName.Trim(), 0xFFFFFFFF);
            }
            catch (Exception e)
            {
                if (e.Message == "ALREADY_EXIST")
                {
                    requestedObject = theList[IndexOfThisObject(theList, ObjectName)];
                    if (requestedObject.Size == 0)  // LOCK the OBJECT.
                        requestedObject.Size = 0xFFFFFFFF;
                    else if ((requestedObject.Size >> 31) == 1) // This OBJECT is being parsed now.
                        requestedObject = null;
                }
                else if (e.Message == "NULL_LIST")
                    throw e;
                else
                {
                    System.Windows.Forms.MessageBox.Show("Failed to Create a new KERNEL_OBJECT for " + ObjectName, "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                    requestedObject = null;
                }
            }

            return requestedObject;
        }

        public void InitializeList()
        {
            string[] txtFiles = Directory.GetFiles(Directory.GetCurrentDirectory(), "MR_*.txt", SearchOption.AllDirectories);

            if (txtFiles.Length > 0)
            {
                foreach (string fileName in txtFiles)
                {
                    ParsingStarter(fileName);
                }

                // For Test...
                //foreach (KERNEL_OBJECT obj in Registered)
                //{
                //    //if (obj.UnnamedObjects != null)
                //    //{
                //    obj.ShowFieldsInfo();
                //    //}
                //}
            }
        }

        private static void ParsingStarter(string FileName)
        {
            StreamReader sr = null;

            if ((FileName != null) && File.Exists(FileName))
            {
                try
                {
                    sr = new StreamReader(new FileStream(FileName, FileMode.Open));
                }
                catch (System.Security.SecurityException)
                {
                    System.Windows.Forms.MessageBox.Show("Access Denied : " + FileName, "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                }

                if (sr != null)
                {
                    string currentLine = null;
                    KERNEL_OBJECT currentObject = null;

                    try
                    {
                        ////////////////////////////////////////////////////////////////////////////////////////////////
                        /////////////////////                  RECOGNIZE OBJECT                    /////////////////////
                        ////////////////////////////////////////////////////////////////////////////////////////////////
                        while ((currentLine !=  null) || (!sr.EndOfStream))
                        {
                            if(currentLine == null)
                                currentLine = sr.ReadLine();

                            if ((currentLine != null) && (currentLine.Contains("!_") && (currentLine.Split(new char[] { '!' }, StringSplitOptions.RemoveEmptyEntries).Length == 2)))
                            {
                                /////////////////////////////////////////////////////////////////////////////////////////////
                                ///////////////////                  GET KERNEL_OBJECT                    ///////////////////
                                /////////////////////////////////////////////////////////////////////////////////////////////
                                currentObject = RequestNewObject(Registered, currentLine.Remove(0, currentLine.IndexOf('!') + 1));
                                if (currentObject == null)
                                {
                                    currentLine = null;
                                    continue;    // PASS : This OBJECT is being parsed now or Failed to create a new KERNEL_OBJECT instance.
                                }

                                //if (currentObject.Fields == null) // CHANGED -> If the Same OBJECT has already been parsed, Check it one more time.
                                //{
                                    currentLine = KernelObjectParser(sr, currentObject);

                                    if ((currentLine != null) && currentLine.StartsWith(false.ToString()))
                                    {
                                        List<string> debuggingContext = new List<string>();
                                        debuggingContext.Clear();

                                        string[] failedObject = currentLine.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                        debuggingContext.Add("Failed to StreamReader.ReadLine() in Parsing the [" + failedObject[1] + "].");
                                        if (failedObject.Length > 2)
                                        {
                                            debuggingContext.Add("  - Cancelled with :");
                                            for (int i = 2; i < failedObject.Length; i++)
                                                debuggingContext.Add("      " + failedObject[i]);
                                        }
                                        DebuggingForm debuggingForm = new DebuggingForm("Error In Parsing", debuggingContext.ToArray(), System.Windows.Forms.MessageBoxButtons.OK);
                                        debuggingForm.ShowDialog();

                                        currentLine = null;
                                    }
                                    else
                                    {
                                        //  Succeeded to Parse.
                                        if ((currentObject.Fields != null) && ((currentObject.Size >> 31) == 1))
                                            currentObject.EndOfObject();
                                    }
                                //}
                                //else
                                //{
                                //    // The Same Object has already been parsed & Registered.
                                //    currentLine = null;
                                //}
                            }
                            else
                                currentLine = null;
                        }
                    }
                    catch (System.OutOfMemoryException)
                    {
                        System.Windows.Forms.MessageBox.Show("Insufficient Memory for Buffer.", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                    }
                    catch (System.IO.IOException)
                    {
                        System.Windows.Forms.MessageBox.Show("IO Exception.", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                    }

                    sr.Close();
                }
            }
            else
                System.Windows.Forms.MessageBox.Show("Unable to locate file \"" + FileName + "\"", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);

            return;
        }

        /// <summary>
        /// Recursive Ver.
        /// </summary>
        /// <param name="sr"></param>
        /// <param name="ObjectName"></param>
        /// <returns>The Last Line</returns>
        private static string KernelObjectParser(StreamReader sr, KERNEL_OBJECT currentObject, string currentLine = null)
        {
            List<string> FailedLines = new List<string>();
            KOBJECT_FIELD currentField = null;
            string lastFieldName = null;
            short firstDepth = -1;
            short depth = -1;
            int searchIndex = 0;
            bool noParsingForThisObject = true;
            //bool isUnnamedTag = false;    // Updated it : PASS -> PARSE.

            if (currentObject != null)
            {
                if (currentObject.Fields == null)
                {
                    currentObject.Fields = new List<KOBJECT_FIELD>();
                    currentObject.Fields.Clear();
    
                    noParsingForThisObject = false;

                    // LOCK the OBJECT of whitch the size has already been known.
                    currentObject.Size |= ((uint)1 << 31);   // Include 0xFFFFFFFF
                }
            }

            FailedLines.Clear();
            while ((currentLine != null) || (!sr.EndOfStream))
            {
                if (currentLine == null)
                {
                    try
                    {
                        currentLine = sr.ReadLine();
                    }
                    catch (Exception)
                    {
                        if (currentObject != null)
                        {
                            if (!noParsingForThisObject)
                            {
                                // UNLOCK.
                                if (currentObject.Size == 0xFFFFFFFF)
                                    currentObject.Size = 0;
                                else if ((currentObject.Size >> 31) == 1)
                                    currentObject.Size &= (~((uint)1 << 31));

                                currentObject.Fields.Clear();
                                currentObject.Fields = null;
                            }
                        }

                        // In this case, Terminate the whole PARSING.
                        sr.BaseStream.Seek(0, SeekOrigin.End);
                        return (false.ToString() + " " + currentObject.Name);
                    }
                }
                

                depth = (short)currentLine.IndexOf('+');
                if (depth > 0)
                {
                    if (firstDepth < 0)     // Set my Depth.
                        firstDepth = depth;

                    ///////////////////////////////////////////////////////////////////////////////////////////////
                    //////////////////////                  PARSING START                    //////////////////////
                    ///////////////////////////////////////////////////////////////////////////////////////////////
                    if (depth < firstDepth)
                    {
                        // End of My OBJECT.
                        if (noParsingForThisObject)
                            return currentLine;
                        else
                            break;
                    }
                    else if (depth > firstDepth)
                    {
                        // It's for the new OBJECT.
                        //if (noParsingForThisObject/* || isUnnamedTag*/)
                        //    currentLine = null;
                        //else
                        //{

                        if (noParsingForThisObject)
                        {
                            if (currentObject == null)
                            {
                                // This OBJECT is being parsed now. -> PASS.
                                currentLine = null;
                                continue;
                            }
                            else
                            {
                                if((currentObject.Fields != null) && (lastFieldName != null))
                                {
                                    for (; searchIndex < currentObject.Fields.Count; searchIndex++)
                                    {
                                        if (currentObject.Fields[searchIndex].Name == lastFieldName)
                                        {
                                            currentField = currentObject.Fields[searchIndex];
                                            break;
                                        }
                                    }

                                }
                            }
                        }

                        if ((currentField != null) && (((currentField.FieldType >> 24) & 7) > 1))    // Except [0 : Default Type | 1 : PADDING FIELD]
                        {
                            KERNEL_OBJECT subObject = null;

                            if (((currentField.FieldType >> 24) & 7) == 2)   // '<unnamed-tag> Object' Type .
                            {
                                if (currentObject.UnnamedObjects == null)
                                {
                                    currentObject.UnnamedObjects = new List<KERNEL_OBJECT>();
                                    currentObject.UnnamedObjects.Clear();
                                }

                                subObject = RequestNewObject(currentObject.UnnamedObjects, currentField.Name);

                                //currentLine = null;
                                //isUnnamedTag = true;
                                //continue;
                            }
                            else
                                subObject = RequestNewObject(Registered, currentField.Description.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Last());

                            currentLine = KernelObjectParser(sr, subObject, currentLine);
                            

                            //////////////////////////////////////////////////////////////////////////////////////
                            ///////////////         The End of the SUB-OBJECT's Parsing.           ///////////////
                            //////////////////////////////////////////////////////////////////////////////////////
                            // Error In 'sr.ReadLine()'
                            if ((currentLine != null) && currentLine.StartsWith(false.ToString()))
                            {
                                if (!noParsingForThisObject)
                                {
                                    // UNLOCK.
                                    if (currentObject.Size == 0xFFFFFFFF)
                                        currentObject.Size = 0;
                                    else if ((currentObject.Size >> 31) == 1)
                                        currentObject.Size &= (~((uint)1 << 31));

                                    currentObject.Fields.Clear();
                                    currentObject.Fields = null;
                                    currentLine += (" " + currentObject.Name);
                                }

                                return currentLine;
                            }

                            // Succeeded to Parse the SubObject.
                            if ((subObject != null) && (subObject.Fields != null) && ((subObject.Size >> 31) == 1))
                            {
                                uint ObjectSize = 0;

                                // If the Parent field of this SUBOBJECT is not the POINTER type, Send the size of this field as the SUBOBJECT's Size.
                                if ((currentLine != null) && (((currentField.FieldType >> 24) & 8) == 0))
                                {
                                    if (currentLine.IndexOf('+') == firstDepth)
                                    {
                                        ObjectSize = (Convert.ToUInt16(currentLine.Split(new char[] { '+', ':' }, StringSplitOptions.RemoveEmptyEntries)[1].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[0].Remove(0, 2), 16));
                                        if (ObjectSize > currentField.Offset)
                                            ObjectSize -= currentField.Offset;
                                        else
                                            ObjectSize = 0;
                                    }
                                }

                                subObject.EndOfObject(ObjectSize);

                                // '<unnamed-tag> Object' Type.
                                if (!noParsingForThisObject && (((currentField.FieldType >> 24) & 7) == 2))
                                    currentField.SetUnnamedObjectField(subObject.Size);

                                // For Test...
                                //currentObject.ShowFieldsInfo();
                            }

                        }   
                        //}
                    }
                    else {
                        // It's for my OBJECT.
                        //if (isUnnamedTag)
                        //    isUnnamedTag = false;

                        //if (noParsingForThisObject)
                        //    currentLine = null;
                        //else
                        //{     
                        string[] splitLine = currentLine.Split(new char[] { '+', ':' }, StringSplitOptions.RemoveEmptyEntries);
                        if (splitLine.Length == 3)
                        {
                            string[] offsetNname = splitLine[1].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            if (offsetNname.Length == 2)
                            {
                                currentField = null;
                                lastFieldName = offsetNname[1];
                                
                                if (!noParsingForThisObject)
                                {
                                    try
                                    {
                                        currentField = new KOBJECT_FIELD(Convert.ToUInt16(offsetNname[0].Remove(0, 2), 16), lastFieldName, splitLine[2]);
                                        currentObject.AddField(currentField);
                                    }
                                    catch (Exception e)
                                    {
                                        if (e.Message == "UNKNOWN_SYMBOLS_FOUND")
                                        {
                                            FailedLines.Add("[UNKNOWN SYMBOLS] " + currentLine);
                                        }
                                        else {
                                            System.Windows.Forms.MessageBox.Show("Error in Parsing", e.Message, System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                                            FailedLines.Add("[Allocation Failed] " + currentLine);
                                        }
                                        currentField = null;
                                    }
                                }

                                currentLine = null;
                                continue;
                            }
                        }

                        // Failed Lines...
                        currentField = null;
                        lastFieldName = null;

                        FailedLines.Add(currentLine);
                        currentLine = null;
                        //}
                    }
                }
                else
                {
                    if (currentLine.Contains("!_"))
                        break;
                    else {
                        if (currentLine.Trim().Length != 0)
                            FailedLines.Add(currentLine);
                        currentLine = null;
                    }
                }
            }

            //////////////////////////////////////////////////////////////////////////////////////////////
            /////////////////                  PARSING COMPLETE                    ///////////////////////
            //////////////////////////////////////////////////////////////////////////////////////////////
            if (!noParsingForThisObject)
            {
                if (currentObject.Fields.Count == 0)
                    currentObject.Fields = null;

                if (FailedLines.Count > 0)
                {
                    DebuggingForm debuggingForm = new DebuggingForm("Error in Parsing : " + currentObject.Name, FailedLines.ToArray(), System.Windows.Forms.MessageBoxButtons.YesNo, "Do you want to register the " + currentObject.Name + " without these fields?");
                    if (debuggingForm.ShowDialog() == System.Windows.Forms.DialogResult.No)
                    {
                        // DESTROY.
                        currentObject.Fields.Clear();
                        currentObject.Fields = null;
                    }
                    else
                    {
                        // These Fields will be represented as the PADDING FIELDs.
                    }
                }

                // UNLOCK.
                if (currentObject.Fields == null)
                {
                    if (currentObject.Size == 0xFFFFFFFF)
                        currentObject.Size = 0;
                    else if ((currentObject.Size >> 31) == 1)
                        currentObject.Size &= (~((uint)1 << 31));
                }
            }
        
            return currentLine;
        }



        /////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////           INTERFACE           /////////////////////////////
        ///////////////////////////////////////////////////////////////////////////////////////// 
        internal uint GetObjectSize(string ObjectName)
        {
            if ((ObjectName != null) && (IndexOfThisObject(Registered, ObjectName) != -1))
                return Registered[IndexOfThisObject(Registered, ObjectName)].Size;
            else
                return 0;
        }

        internal static void AddFileToParse(string FileName)
        {
            if (!isParsingNewFile)
            {
                // For Test...
                //SHOW_DEBUGGING_MESSAGE_FOR_OVERWRITTING_THE_OBJECT_SIZE = true;

                // LOCK.
                isParsingNewFile = true;
                ParsingStarter(FileName);
                isParsingNewFile = false;

                // For Test...
                //SHOW_DEBUGGING_MESSAGE_FOR_OVERWRITTING_THE_OBJECT_SIZE = false;
            }
            else
                System.Windows.Forms.MessageBox.Show("The previous parsing hasn't been finished yet.\r\nTry it later.", "Warning", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);

            return;
        }

        internal static string[] GetRegisteredObjectsList()
        {
            if(Registered.Count > 0)
            {
                List<string> nameList = new List<string>();
                List<string> sizeList = new List<string>();
                int maxLength = 0;

                foreach (KERNEL_OBJECT currentObject in Registered)
                {
                    if ((currentObject.Fields != null) && (currentObject.Fields.Count > 0) && ((currentObject.Size >> 31) == 0))
                    {
                        sizeList.Add(String.Format("0x{0:X}", currentObject.Size));
                        nameList.Add(currentObject.Name);

                        if (maxLength < currentObject.Name.Length)
                            maxLength = currentObject.Name.Length;
                    }
                }

                if (maxLength > 0)
                {
                    string format = " {0,-" + maxLength + "}  [{1,6}]";
                    string[] result = new string[nameList.Count];

                    for(int i = 0; i < nameList.Count; i++)
                        result[i] = String.Format(format, nameList[i], sizeList[i]);

                    return result;
                }
            }

            return null;
        } 

        //////////////////////////////////////////////////////////////////
    }
}
