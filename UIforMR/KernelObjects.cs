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
            public ushort Offset { get; }
            /*                                                                                                                                              (Max Value)
            00		        0		    0 		    0		    000 			        00		        00 0000 0000		0000 00    	    00 0000
            UNION           GRANULARITY	ARRAY		POINTER     DATA TYPE		        OUTPUT TYPE	    Resv.               Value1          Value2
            ============================================================================================================================================================
            10 : Union	    1 : BIT		-		    -		    -			            		        -                   Position(64)	Bits(64)
            11 : Union-Start0 : BYTE	1 : ARRAY                                       <------- Array Count(4,096) ------->
                        		                    1 : POINTER 010 : 64-bits Address

								                                111 : Another OBJECT                                        <----------- OBJECT SIZE(4,096) ----------->		
								                                110 : LIST_ENTRY                        ->                                  00 0001 : SINGLE_LIST_ENTRY			
								                                101 : LARGE_INTEGER                     ->                                  01 0000 : ULARGE_INTEGER                                                        
								                                011 : UNICODE_STRING                    ->                                  00 0001 : [ANSI_]STRING
								                                010 : <unnamed-tag>                     ->                                  SIZE(64)
    
								                                000 : Default DATA TYPE					                                    //10 0001 : STRING[ASCII]
															                                                                                //10 0010 : STRING[UNICODE]

															                                                                                00 ____ :   signed_
															                                                                                01 ____ : unsigned_
															                            00 : HEXA                                           __ 0001 :           _CHAR
                                                                                        01 : BINARY                                         __ 0010 :	        _INT2B
											                                            10 : DECIMAL			                            __ 0100 : 	        _INT4B
											                                            11 : DATE			                                __ 1000 : 	        _INT8B

                                                                                                                                            11 0000	: Void

            */
            private uint fieldType;
            internal uint FieldType { get { return fieldType; } }

            internal KOBJECT_FIELD(ushort offset, string name, string description)
            {
                Offset = offset;
                Name = name;
                Description = description;

                fieldType = InitializeFieldType();
            }


            private uint InitializeFieldType()
            {
                uint result = 0;
                //bool isUnknown = false;
    
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
                        result |= (0x02 << 24);
                    else
                    {
                        foreach (string key in keys)
                        {
                            if (key.First() == '_')
                            {
                                switch (key.Remove(0, 1))
                                {
                                    case "list_entry":
                                        result |= (0x06 << 24);
                                        break;
                                    case "single_list_entry":
                                        result |= (0x06 << 24);
                                        result |= 0x01;
                                        break;
                                    case "large_integer":
                                        result |= (0x05 << 24);
                                        break;
                                    case "ularge_integer":
                                        result |= (0x05 << 24);
                                        result |= 0x10;
                                        break;
                                    case "unicode_string":
                                        result |= (0x03 << 24);
                                        break;
                                    case "ansi_string":
                                        result |= (0x05 << 24);
                                        result |= 0x1;
                                        break;
                                    default:
                                        result |= (0x07 << 24);
                                        break;
                                }
                            }
                            else if (key.StartsWith("ptr"))
                            {
                                result |= (0x08 << 24);

                                if (key.EndsWith("64"))
                                    result |= (0x02 << 24);
                                else
                                {
                                    if (!key.EndsWith("32"))
                                    {
                                        //isUnknown = true;
                                        //result = 0;
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
                                result |= (Convert.ToUInt32(key.Trim(new char[] { '[', ']' })) << 12);
                            }
                            else if (key == "void")
                                result |= 0x30;
                            else
                            {
                                //isUnknown = true;
                                //result = 0;     // 요거 좀 더 생각해 보자.. 지울지 냅둘지.
                            }
                        }

                    }
                }

                return result;
            }




            /// <summary>
            /// </summary>
            /// <param name="flagsForUnion">
            /// 7-bit : UNION
            /// 6-bit : UNION-START
            /// 3-bit : REPLACE to UNION .
            /// 2-bit : Result of CalculateUnionFieldSize().
            /// 1-bit : This CALL is just for retrieving the Field Size.
            /// 0-bit : THE LAST FIELD
            /// </param>
            /// <param name="fieldSize">
            /// Next Field's Offset - Current Field's Offset
            /// </param>
            /// <returns>
            /// 0 : Failed. 
            /// Others : Calculated Size.
            /// </returns>
            internal uint IntermediateProcessingFieldType(byte flagsForUnion, ushort fieldSize)
            {
                bool isLastField = (flagsForUnion & 0x1) == 0? false: true;
                bool isArray = (this.FieldType & (1 << 28)) == 0 ? false : true;
                uint calculatedSize = 0;

                // Marking the UNION Flags.
                if((flagsForUnion & 6) == 0)
                    this.fieldType |= (((uint)(flagsForUnion & 0xC0)) << 24);

                // Just for Replacement.
                if((flagsForUnion & 0x8) != 0)
                    return 0;       // Not Use.


                //////////////////////////////////////////////////////////////////////////////
                ////////////////               Calculate Size                 ////////////////
                //////////////////////////////////////////////////////////////////////////////
                if((this.FieldType & ((uint)0x2 << 28)) != 0)
                {
                    // 1. BIT
                    // calculatedSize = 0;
                }
                else if ((this.FieldType & ((uint)0x8 << 24)) != 0)
                {
                    // 1. POINTER :                
                    calculatedSize = 4;
                    if ((this.FieldType & ((uint)0x2 << 24)) != 0)
                        calculatedSize = calculatedSize << 1;
                }
                else
                {
                    // 2. DATA TYPE :
                    switch ((this.FieldType >> 24) & 0x7)
                    {
                        case 0:
                            // Default Date Type
                            calculatedSize = (this.FieldType & 0xF);
                            break;
                        case 2:
                            // <unnamed-tag>
                            //////////////////////////////////////////////////////////////////////
                            // 해당 경우에도 필드 사이즈 정확히 판단 불가함. -> LastProcessing으로 같이 처리할 것. 일단 재귀 함수 버젼으로...
                            if (flagsForUnion == 2)
                                calculatedSize = (this.FieldType & 0x3F);
                            else
                            {
                                calculatedSize = (((uint)fieldSize) & 0x3F);
                                this.fieldType |= calculatedSize;          // Mark direct.
                            }
                            break;
                        case 3:
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
                            if ((this.FieldType & 0x1) != 0)     // SINGLE_LIST_ENTRY
                                calculatedSize = calculatedSize >> 1;
                            break;
                        case 7:
                            // Another OBJECT
                            if (flagsForUnion == 2)
                                calculatedSize = (this.FieldType & 0xFFF);
                            else
                            {
                                // First, Search for the Name at the Registered List. 
                                // If The Name is not found, The Size will be caculated using the 'fieldsize'. 
                                int tmp = IndexOfThisObject(this.Description.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Last());
                                if (tmp != -1)
                                    calculatedSize = Registered[tmp].Size;
                                else
                                {
                                    calculatedSize = fieldSize;

                                    // If this field is an array, divide with the Array Count.
                                    if (isArray)
                                        calculatedSize /= (this.FieldType & 0xFFF000);

                                    ///////////////////////////////// 여기서 필드사이즈가 0이 아닌 경우, KERNEL_OBJECT Light Ver. 만들어서 Registered에 등록할 것.
                                    // 만약, 0이라면 아래 만들고 있는 유니온 내부 필드의 사이즈 계산 함수에서 사이즈 계산해오는 걸로..... 
                                    //      -> 애초에 이 함수 콜하는 애를 따로 만들고, CalculateUnionFieldSize() 후에 Registered등록 바로 하는 걸로...
//                                    if (calculatedSize == 0)


                                }

                                // Mark the Size.
                                if (calculatedSize < 0x1000)
                                    this.fieldType |= calculatedSize;
                            }
                            break;
                        default:
                            // NONE
                            break;
                    }
                }


                if (isArray)
                    calculatedSize *= (this.FieldType & 0xFFF000);


                //////////////////////////////////////////////////////////////////////////////
                //////////////////////////////////////////////////////////////////////////////
                //////////////////////////////////////////////////////////////////////////////
//                if ((flagsForUnion == 2) || isLastField)
                    return calculatedSize;

                /*
                아래 적힌 거 하는 중... 위에 리턴 값 0일 때, 비트 타입인 경우 후처리해야 함.
                    -> 이거 EndOfFields() 끝나고 다른 함수에서 처리해야할 듯.
                        -> 유니온인 경우, 같은 유니온 내에서 가장 사이즈 큰 거 찾아야 할듯....
                            -> EndOfFileds 함수 밑에 만드는 중.....

                리턴값 사이즈로 통일...
                EndOfFields 에서 라스트 필드 리턴 값 제대로 받으면, offset과 더해서 오브젝트 사이즈 입력.
                    -> 이거 좀더 생각해볼 것.
                   

                라스트 필드 플래그는 출력 플래그로..
	                -> 이 때는 fieldSize 값을 fieldType로 입력하는 코드 다 막아야 함.
                        -> 이거 완료.

                일단 중지. 재귀 함수 버젼으로 다시 만들 것.
                    마지막 수정시, 2bit를 유니온 필드 사이즈 계산용으로 바꾸던 중이여서 좀 꼬였음....
                    만약, 이거 다시 사용할 거라면 2bit 관련해서 체크 쭉 해야함.
                */

                //else
                //{
                //    // For Error Check.  
                //    if (calculatedSize == fieldSize)
                //        return 1;
                //    else
                //    {
                //        // Ex.Container Alignment  ////////////////////////////////////////////////////////  패딩 필드 추가하자...
                //        return 0;
                //    }
                        
                //}
            }
                
                
                
                            
            //internal void SetMe(KERNEL_OBJECT KernelObject)
            //{
            //    MyObject = KernelObject;
            //}

            //internal void SetFieldSize(ushort nextOffset)
            //{
            //    TypeNSize |= (ushort)(nextOffset - Offset);
            //}


            //internal byte GetOutputType()
            //{
            //    return (byte)(TypeNSize >> 12);
            //}


            public void Show()
            {
                System.Windows.Forms.MessageBox.Show(String.Format("+0x{0:X3} {1,-17}: {2}", Offset, Name, Description), "Field Info", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
            }

            public override string ToString()
            {
                string tmp;

                if ((this.FieldType & (0x8 << 28)) != 0)
                {
                    if ((this.fieldType & (0x4 << 28)) != 0)
                        tmp = " -";
                    else
                        tmp = "  +";
                }
                else
                    tmp = " +";

                return tmp + String.Format("0x{0:X3} {1,-17}: {2}", Offset, Name, Description);
            }
        }

        public class KERNEL_OBJECT
        {
            public string Name { get; }
            public uint Size { get; set; }
            public List<KOBJECT_FIELD> Fields { get; set; }

            internal KERNEL_OBJECT(string name)
            {
                Name = name;
                Size = 0;

                Fields = new List<KOBJECT_FIELD>();
                Fields.Clear();
            }

            /// <summary>
            /// Light Ver.
            /// It is created by "KOBJECT_FIELD.CompleteFieldType()" and Only 2 Fields are available; "Name" & "Size". 
            /// The "Fields" stores 'null' and It can be filled by "KernelObjects.KernelObjectParser()" later. 
            /// </summary>
            /// <param name="name"></param>
            /// <param name="size"></param>
            internal KERNEL_OBJECT(string name, uint size)
            {
                Name = name;
                Size = size;

                Fields = null;
            }

            internal bool AddField(KOBJECT_FIELD field)
            {
                if(Fields != null)
                {
                    //field.SetMe(this);
    
                    //if (Fields.Count > 0)
                    //{
                    //    //field.Size
                    //   //    Fields.Last().Offset
                    //}

                    Fields.Add(field);
                    return true;
                }

                return false;
            }

            internal void EndOfField()
            {
                byte flagsForUnion = 0;
                ushort fieldSize = 0;
                int i, j = 0;
                ushort minOffset, maxOffset;

                // Except the Last Field.
                for(i = 0; i < Fields.Count - 1; i++)
                {          
                    if (Fields[i].Offset < Fields[i + 1].Offset)
                    {
                        fieldSize = (ushort)(Fields[i + 1].Offset - Fields[i].Offset);
                        if ((flagsForUnion & 0x80) != 0)
                        {
                            // Always, i > 0
                            if (Fields[i - 1].Offset < Fields[i].Offset)   
                                flagsForUnion = 0; // It's the End of UNION. It can be replaced...
                            else
                                flagsForUnion = 0x80;
                        }

                        Fields[i].IntermediateProcessingFieldType(flagsForUnion, fieldSize);
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
                                flagsForUnion = 0xC0;
                        }
                            

                        Fields[i].IntermediateProcessingFieldType(flagsForUnion, 0);
                    }
                    else
                    {
                        // Replace the UNION flags.
                        minOffset = Fields[i + 1].Offset;
                        maxOffset = Fields[i].Offset;

                        // Search for the Minimum Offset. 
                        for (j = 0; j < i; j++) 
                        {
                            if (Fields[j].Offset >= minOffset)
                                break;
                        }

                        // Replace.
                        if ((Fields[j].FieldType >> 31) == 0)
                            Fields[j].IntermediateProcessingFieldType(0xC8, 0);
                        
                        while(++j < i) {
                            if ((Fields[j].FieldType >> 30) != 0x2)
                                Fields[j].IntermediateProcessingFieldType(0x88, 0);
                        }

                        // NOW : j == i     -> From now on, It is the First Call & Every Field is in UNION.
                        flagsForUnion = 0x80;    
                        
                        while (i < Fields.Count - 1)
                        {
                            if (Fields[i].Offset < Fields[i + 1].Offset)
                                fieldSize = (ushort)(Fields[i + 1].Offset - Fields[i].Offset);
                            else
                                fieldSize = 0;

                            Fields[i].IntermediateProcessingFieldType(flagsForUnion, fieldSize);

                            if (Fields[i + 1].Offset > maxOffset)
                                break;
                            else
                                i++;
                        }

                        // If ((Fields[i].Offset > maxOffset) && (i == Fields.Count - 1))
                        //      -> The Last Field of this OBJECT is in the UNION.
                        if (i == Fields.Count - 1)
                        {
                            Fields[i].IntermediateProcessingFieldType(0x81, 0);
                            return;
                        }
                    }
                }

                // The Last Field of this OBJECT.
                if (Fields[i - 1].Offset >= Fields[i].Offset)
                    flagsForUnion = 0x81;
                else
                    flagsForUnion = 0x1;

                Fields[i].IntermediateProcessingFieldType(flagsForUnion, 0);

              //  LastProcessingForFields();
            }

            private void LastProcessingForFields()
            {
                uint fieldSize = 0;
                uint maximumUnionSize = 0;
                int targetIndex = 0;
                int firstIndexOfThisUNION = 0;
                
                // Include the Last Field.
                do
                {
                    if ((this.Fields[targetIndex].FieldType >> 30) == 3)
                    {
                        firstIndexOfThisUNION = targetIndex;
                        maximumUnionSize = 0;
                        do
                        {                            
                            if ((((this.Fields[targetIndex].FieldType >> 24) & 7) == 7) && ((this.Fields[targetIndex].FieldType & 0xFFF) == 0))
                            {
                                do
                                {
                                    fieldSize = this.Fields[firstIndexOfThisUNION].Offset + this.Fields[firstIndexOfThisUNION].IntermediateProcessingFieldType(0x02, 0);
                                    if (maximumUnionSize < fieldSize)
                                        maximumUnionSize = fieldSize;


                                } while (++firstIndexOfThisUNION < targetIndex);
                                // 위에 조건에서 맥시멈값 - targetOffset == SIZE
                                // 만약, 맥시멈이 targetOffset 보다 작으면, targetINdex 이후조건 돌려야할 듯...
                                // 다시 보자.
                                // 일단 여기서 중지.   재귀함수 버젼으로 다시 만들 것. 
                                // firstIndexOfThisUNION == targetIndex
                            }
                        } while ((++targetIndex < this.Fields.Count) && ((this.Fields[targetIndex].FieldType >> 30) == 2));
                        
                    }
                    

                } while (++targetIndex < this.Fields.Count);
            }

            internal void ShowFieldsInfo()
            {
                string[] fieldsInfo = new string[this.Fields.Count];
                for (int i = 0; i < this.Fields.Count; i++)
                {
                    fieldsInfo[i] = this.Fields[i].ToString();
                }

                DebuggingForm debuggingForm = new DebuggingForm("Type Info : " + this.Name, fieldsInfo, System.Windows.Forms.MessageBoxButtons.OK);
                debuggingForm.ShowDialog();
            }
        }

        //////////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////

        internal volatile static List<KERNEL_OBJECT> Registered = null;
        private MainForm form = null;
        private Thread ParsingThread = null;

        public KernelObjects(MainForm f)
        {
            form = f;

            Registered = new List<KERNEL_OBJECT>();
            Registered.Clear();

            ParsingThread = new Thread(InitializeList);
            ParsingThread.Start();
        }

        public void InitializeList()
        {
            string[] txtFiles = Directory.GetFiles(Directory.GetCurrentDirectory(), "MR_*.txt", SearchOption.AllDirectories);

            if (txtFiles.Length > 0)
            {
                foreach (string fileName in txtFiles)
                {
                    KernelObjectParser(fileName); 
                }
            }
        }

        internal bool KernelObjectParser(string FileName)
        {
            bool lightVerExist = false;

            if ((FileName != null) && File.Exists(FileName))
            {
                StreamReader sr = null;

                try
                {
                    sr = new StreamReader(new FileStream(FileName, FileMode.Open));
                }
                catch (System.Security.SecurityException)
                {
                    System.Windows.Forms.MessageBox.Show("Access Denied : " + FileName, "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                }

                if(sr != null)
                {
                    try
                    {
                        string currentLine = null;

                        ////////////////////////////////////////////////////////////////////////////////////////////////
                        /////////////////////                  RECOGNIZE OBJECT                    /////////////////////
                        ////////////////////////////////////////////////////////////////////////////////////////////////
                        while (!sr.EndOfStream)
                        {
                            currentLine = sr.ReadLine();
                            if ((currentLine != null) && (currentLine.Contains("!_")))
                            {
                                KERNEL_OBJECT currentObject = null;

                                currentLine = currentLine.Remove(0, currentLine.IndexOf('!') + 1);
                                if (Registered.Count > 0)
                                {
                                    int index = IndexOfThisObject(currentLine);
                                    if(index != -1)
                                    {
                                        currentObject = Registered[index];
                                        if(currentObject.Fields != null)
                                        {
                                            System.Windows.Forms.MessageBox.Show(currentLine + "is already registered.", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                                            break;
                                        }
                                        else
                                        {
                                            currentObject.Fields = new List<KOBJECT_FIELD>();
                                            currentObject.Fields.Clear();

                                            lightVerExist = true;
                                        }
                                    }
                                }
                               
                                if(!lightVerExist)
                                    currentObject = new KERNEL_OBJECT(currentLine);


                                ////////////////////////////////////////////////////////////////////////////////////////////////
                                //////////////////////                  FIELDS PARSING                    //////////////////////
                                ////////////////////////////////////////////////////////////////////////////////////////////////
                                KOBJECT_FIELD currentField = null;
                                sbyte depth = -1;

                                // For Test.... 
                                List<string> errorLine = new List<string>();
                                errorLine.Clear();
                                
                                while (!sr.EndOfStream)
                                {
                                    currentLine = sr.ReadLine();

                                    if (depth == -1)
                                        depth = (sbyte)currentLine.IndexOf('+');


                                    //if ((currentLine.Length > 0) && (currentLine.Contains('+')) && (currentLine.Contains(':')))
                                    if ((sbyte)currentLine.IndexOf('+') == depth)
                                    {
                                        string[] tmp = currentLine.Split(new char[] { '+', ':' }, StringSplitOptions.RemoveEmptyEntries);
                                        if (tmp.Length == 3)
                                        {
                                            string[] offsetNname = tmp[1].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                            if (offsetNname.Length == 2)
                                            {
                                                currentField = new KOBJECT_FIELD(Convert.ToUInt16(offsetNname[0].Remove(0, 2), 16), offsetNname[1], tmp[2]);
                                                if (currentField != null)
                                                {
                                                    // For Test...
                                                    //currentField.Show();
                                                    //goto PARSE_ONLY_ONE;

                                                    currentObject.AddField(currentField);
                                                    currentField = null;
                                                    continue;
                                                }
                                            }
                                        }
                                    }

                                    // For Test...
                                    errorLine.Add(currentLine);
                                }

                                //////////////////////////////////////////////////////////////////////////////////////////////
                                /////////////////                  PARSING COMPLETE                    ///////////////////////
                                //////////////////////////////////////////////////////////////////////////////////////////////

                                // For Test...
                                //PARSE_ONLY_ONE:
                                errorLine.Add("Test for ErrorLine...");
                                
                                sr.Close();
                                if (errorLine.Count > 0)        
                                {
                                    DebuggingForm debuggingForm = new DebuggingForm("Error in Parsing", errorLine.ToArray(), System.Windows.Forms.MessageBoxButtons.YesNo, "Do you want to register the " + currentObject.Name + " without these fields?");
                                    if(debuggingForm.ShowDialog() == System.Windows.Forms.DialogResult.No)
                                    {
                                        if (lightVerExist)
                                            currentObject.Fields.Clear();
                                        
                                        return false;
                                    }
                                }

                                currentObject.EndOfField();     // 이거 만들고 나서 부족한 거 있나 살펴봐야함.

                                // For Test...
                                currentObject.ShowFieldsInfo();

                                //if (lightVerExist)
                                //{
                                //    // Size Check
                                //}
                                //else
                                //    Registered.Add(currentObject);




                                return true; 
                            }
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

            return false;
        }

        /// <summary>
        /// If the object is not found, return -1
        /// </summary>
        /// <param name="ObjectName"></param>
        /// <returns></returns>
        public static int IndexOfThisObject(string ObjectName)
        {
            if(Registered.Count > 0)
            {
                int i = 0;

                for (i = 0; i < Registered.Count; i++)
                {
                    if (Registered[i].Name.ToLower().Trim() == ObjectName.ToLower().Trim())
                        return i;
                }
            }

            return -1;
        }

   

    }
}
