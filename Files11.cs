// Files11.cs
// Copyright � 2019 Kenneth Gober
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.


// Files-11 File System Structure
//
// http://www.bitsavers.org/pdf/dec/pdp11/rsx11/Files-11_ODS-1_Spec_Jun75.pdf
// http://www.bitsavers.org/pdf/dec/pdp11/rsx11/Files-11_ODS-1_Spec_Jun77.pdf
// http://www.bitsavers.org/pdf/dec/pdp11/rsx11/Files-11_ODS-1_Spec_Apr81.pdf
// http://www.bitsavers.org/pdf/dec/pdp11/rsx11/Files-11_ODS-1_Spec_Sep86.txt
//
// Home Block
//  0   H.IBSZ  Index File Bitmap Size (blocks, != 0)
//  2   H.IBLB  Index File Bitmap LBN (2 words, high word first, != 0)
//  6   H.FMAX  Maximum Number of Files (!= 0)
//  8   H.SBCL  Storage Bitmap Cluster Factor (== 1)
//  10  H.DVTY  Disk Device Type (== 0)
//  12  H.VLEV  Volume Structure Level (== 0x0101)
//  14  H.VNAM  Volume Name (padded with nulls)
//  26          (not used)
//  30  H.VOWN  Volume Owner UIC
//  32  H.VPRO  Volume Protection Code
//  34  H.VCHA  Volume Characteristics
//  36  H.FPRO  Default File Protection
//  38          (not used)
//  44  H.WISZ  Default Window Size
//  45  H.FIEX  Default File Extend
//  46  H.LRUC  Directory Pre-access Limit
//  47          (not used)
//  58  H.CHK1  First Checksum
//  60  H.VDAT  Volume Creation Date "DDMMMYYHHMMSS"
//  74          (not used)
//  472 H.INDN  Volume Name (padded with spaces)
//  484 H.INDO  Volume Owner (padded with spaces)
//  496 H.INDF  Format Type 'DECFILE11A' padded with spaces
//  508         (not used)
//  510 H.CHK2  Second Checksum
//
// File Header Area
//  0   H.IDOF  Ident Area Offset (in words)
//  1   H.MPOF  Map Area Offset (in words)
//  2   H.FNUM  File Number
//  4   H.FSEQ  File Sequence Number
//  6   H.FLEV  File Structure Level (must be 0x0101)
//  8   H.FOWN  File Owner UIC
//  10  H.FPRO  File Protection Code
//  12  H.FCHA  File Characteristics
//  14  H.UFAT  User Attribute Area (32 bytes)
//
// File Ident Area
//  0   I.FNAM  File Name (9 characters as 3 Radix-50 words)
//  6   I.FTYP  File Type (3 characters as 1 Radix-50 word)
//  8   I.FVER  Version Number (signed)
//  10  I.RVNO  Revision Number
//  12  I.RVDT  Revision Date 'ddMMMyy'
//  19  I.RVTI  Revision Time 'HHmmss'
//  25  I.CRDT  Creation Date 'ddMMMyy'
//  32  I.CRTI  Creation Time 'HHmmss'
//  38  I.EXDT  Expiration Date 'ddMMMyy'
//  45          (1 unused byte to reach a word boundary)
//
// File Map Area
//  0   M.ESQN  Extension Segment Number (numbered from 0)
//  1   M.ERVN  Extension Relative Volume No.
//  2   M.EFNU  Extension File Number (next header file number, or 0)
//  4   M.EFSQ  Extension File Sequence Number (next header sequence number, or 0)
//  6   M.CTSZ  Block Count Field Size (bytes)
//  7   M.LBSZ  LBN Field Size (bytes)
//  8   M.USE   Map Words In Use
//  9   M.MAX   Map Words Available (1 byte)
//  10  M.RTRV  Retrieval Pointers (M.MAX words)


using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace FSX
{
    partial class ODS1
    {
        // level 0 - check basic disk parameters
        // level 1 - check home block
        public static Int32 CheckVTOC(Disk disk, Int32 level)
        {
            if (disk == null) throw new ArgumentNullException("disk");
            if ((level < 0) || (level > 1)) throw new ArgumentOutOfRangeException("level");

            // level 0 - check basic disk parameters
            if (disk.BlockSize != 512)
            {
                if (Program.Verbose > 1) Console.Error.WriteLine("Disk block size = {0:D0} (must be 512)", disk.BlockSize);
                return -1;
            }

            // ensure disk is at least large enough to contain home block
            if (disk.BlockCount < 2)
            {
                if (Program.Verbose > 1) Console.Error.WriteLine("Disk too small to contain home block");
                return -1;
            }
            if (level == 0) return 0;

            // level 1 - check home block
            Block HB = disk[1];
            if (!HomeBlockChecksumOK(HB, 58))
            {
                if (Program.Verbose > 1) Console.Error.WriteLine("Home Block First Checksum invalid");
                return 0;
            }
            if (!HomeBlockChecksumOK(HB, 510))
            {
                if (Program.Verbose > 1) Console.Error.WriteLine("Home Block Second Checksum invalid");
                return 0;
            }
            if (HB.ToUInt16(0) == 0)
            {
                if (Program.Verbose > 1) Console.Error.WriteLine("Home Block Index File Bitmap Size invalid (must not be 0)");
                return 0;
            }
            if ((HB.ToUInt16(2) == 0) && (HB.ToUInt16(4) == 0))
            {
                if (Program.Verbose > 1) Console.Error.WriteLine("Home Block Index File Bitmap LBN invalid (must not be 0)");
                return 0;
            }
            if (HB.ToUInt16(6) == 0)
            {
                if (Program.Verbose > 1) Console.Error.WriteLine("Home Block Maximum Number of Files invalid (must not be 0)");
                return 0;
            }
            if (HB.ToUInt16(8) != 1)
            {
                if (Program.Verbose > 1) Console.Error.WriteLine("Home Block Storage Bitmap Cluster Factor invalid (must be 1)");
                return 0;
            }
            if (HB.ToUInt16(10) != 0)
            {
                if (Program.Verbose > 1) Console.Error.WriteLine("Home Block Disk Device Type invalid (must be 0)");
                return 0;
            }
            Int32 n = HB.ToUInt16(12);
            if ((n != 0x0101) && (n != 0x0102))
            {
                if (Program.Verbose > 1) Console.Error.WriteLine("Home Block Volume Structure Level invalid (must be 0x0101 or 0x0102)");
                return 0;
            }
            return 1;
        }

        private static Boolean HomeBlockChecksumOK(Block block, Int32 checksumOffset)
        {
            Int32 sum = 0;
            for (Int32 p = 0; p < checksumOffset; p += 2) sum += block.ToUInt16(p);
            Int32 n = block.ToUInt16(checksumOffset);
            if (Program.Verbose > 2) Console.Error.WriteLine("Home block checksum @{0:D0} {1}: {2:x4} {3}= {4:x4}", checksumOffset, ((sum != 0) && ((sum % 65536) == n)) ? "PASS" : "FAIL", sum % 65536, ((sum % 65536) == n) ? '=' : '!', n);
            return ((sum != 0) && ((sum % 65536) == n));
        }

        private static Boolean IsASCIIText(Block block, Int32 offset, Int32 count)
        {
            for (Int32 i = 0; i < count; i++)
            {
                Byte b = block[offset + i];
                if ((b < 32) || (b >= 127)) return false;
            }
            return true;
        }
    }

    partial class ODS1 : FileSystem
    {
        private Disk mDisk;
        private String mDir;
        private UInt16 mDirNum;
        private UInt16 mDirSeq;

        public ODS1(Disk disk)
        {
            mDisk = disk;
            mDir = "[000000]";
            mDirNum = 4;
            mDirSeq = 4;
        }

        public override Disk Disk
        {
            get { return mDisk; }
        }

        public override String Source
        {
            get { return mDisk.Source; }
        }

        public override String Type
        {
            get { return "ODS1"; }
        }

        public override String Dir
        {
            get { return mDir; }
        }

        public override Encoding DefaultEncoding
        {
            get { return Encoding.ASCII; }
        }

        public override void ChangeDir(String dirSpec)
        {
            if ((dirSpec == null) || (dirSpec.Length == 0)) return;
            // special-case [000000] to mean the root directory 4,4,0
            if (String.Compare(dirSpec, "[000000]") == 0)
            {
                mDir = "[000000]";
                mDirNum = 4;
                mDirSeq = 4;
                return;
            }
            // otherwise look for a match in the current directory
            Byte[] data = ReadFile(mDirNum, mDirSeq, 0);
            Int32 bp = 0;
            while (bp < data.Length)
            {
                UInt16 fnum = BitConverter.ToUInt16(data, bp);
                if ((fnum != 0) && (BitConverter.ToUInt16(data, bp + 12) == 0x1a7a)) // 0x1a7a = "DIR" in Radix-50
                {
                    String fn1 = Radix50.Convert(BitConverter.ToUInt16(data, bp + 6));
                    String fn2 = Radix50.Convert(BitConverter.ToUInt16(data, bp + 8));
                    String fn3 = Radix50.Convert(BitConverter.ToUInt16(data, bp + 10));
                    String fn = String.Concat(fn1, fn2, fn3).Trim();
                    fn = String.Concat("[", fn, "]");
                    if (String.Compare(dirSpec, fn, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        mDir = fn;
                        mDirNum = fnum;
                        mDirSeq = BitConverter.ToUInt16(data, bp + 2);
                        return;
                    }
                }
                bp += 16;
            }
        }

        public override void ListDir(String fileSpec, TextWriter output)
        {
            Byte[] data = ReadFile(mDirNum, mDirSeq, 0);
            Int32 bp = 0;
            while (bp < data.Length)
            {
                UInt16 fnum = BitConverter.ToUInt16(data, bp);
                if (fnum != 0)
                {
                    UInt16 fseq = BitConverter.ToUInt16(data, bp + 2);
                    UInt16 fvol = BitConverter.ToUInt16(data, bp + 4);
                    String fn1 = Radix50.Convert(BitConverter.ToUInt16(data, bp + 6));
                    String fn2 = Radix50.Convert(BitConverter.ToUInt16(data, bp + 8));
                    String fn3 = Radix50.Convert(BitConverter.ToUInt16(data, bp + 10));
                    String ext = Radix50.Convert(BitConverter.ToUInt16(data, bp + 12));
                    UInt16 ver = BitConverter.ToUInt16(data, bp + 14);
                    output.WriteLine("{0}{1}{2}.{3};{4:D0} ({5:D0},{6:D0},{7:D0})", fn1, fn2, fn3, ext, ver, fnum, fseq, fvol);
                }
                bp += 16;
            }
        }

        public override void DumpDir(String fileSpec, TextWriter output)
        {
            Byte[] data = ReadFile(mDirNum, mDirSeq, 0);
            Program.Dump(null, data, output, Program.DumpOptions.Radix50);
        }

        public override void ListFile(String fileSpec, Encoding encoding, TextWriter output)
        {
            throw new NotImplementedException();
        }

        public override void DumpFile(String fileSpec, TextWriter output)
        {
            throw new NotImplementedException();
        }

        public override String FullName(String fileSpec)
        {
            throw new NotImplementedException();
        }

        public override Byte[] ReadFile(String fileSpec)
        {
            throw new NotImplementedException();
        }

        private Byte[] ReadFile(UInt16 fileNum, UInt16 seqNum)
        {
            return ReadFile(fileNum, seqNum, 0);
        }

        private Byte[] ReadFile(UInt16 fileNum, UInt16 seqNum, UInt16 volNum)
        {
            if (fileNum == 0) throw new ArgumentOutOfRangeException("fileNum");
            if (volNum != 0) throw new ArgumentOutOfRangeException("volNum");

            // determine size of file
            Int32 n = 0;
            UInt16 hf = fileNum;
            UInt16 hs = seqNum;
            Block H = GetFileHeader(hf, hs, 0);
            while (H != null)
            {
                Int32 map = H[1] * 2; // map area pointer
                Int32 CTSZ = H[map + 6];
                Int32 LBSZ = H[map + 7];
                Int32 q = map + H[map + 8] * 2 + 10; // end of retrieval pointers
                Int32 p = map + 10; // start of retrieval pointers

                // count blocks referenced by file map
                Int32 ct;
                Int32 lbn;
                while (p < q)
                {
                    if ((CTSZ == 1) && (LBSZ == 3)) // Format 1 (normal format)
                    {
                        lbn = H[p++] << 16;
                        ct = H[p++];
                        lbn += H.ToUInt16(ref p);
                    }
                    else if ((CTSZ == 2) && (LBSZ == 2)) // Format 2 (not implemented)
                    {
                        ct = H.ToUInt16(ref p);
                        lbn = H.ToUInt16(ref p);
                    }
                    else if ((CTSZ == 2) && (LBSZ == 4)) // Format3 (not implemented)
                    {
                        ct = H.ToUInt16(ref p);
                        lbn = H.ToUInt16(ref p) << 16;
                        lbn += H.ToUInt16(ref p);
                    }
                    else // unknown format
                    {
                        throw new InvalidDataException();
                    }
                    ct++;
                    n += ct;
                }

                UInt16 nf = H.ToUInt16(map + 2);
                UInt16 ns = H.ToUInt16(map + 4);
                H = (nf == 0) ? null : GetFileHeader(nf, ns, 0);
            }

            // read file
            Byte[] buf = new Byte[n * 512];
            Int32 bp = 0;
            hf = fileNum;
            hs = seqNum;
            H = GetFileHeader(hf, hs, 0);
            while (H != null)
            {
                Int32 map = H[1] * 2; // map area pointer
                Int32 CTSZ = H[map + 6];
                Int32 LBSZ = H[map + 7];
                Int32 q = map + H[map + 8] * 2 + 10; // end of retrieval pointers
                Int32 p = map + 10; // start of retrieval pointers

                // read blocks referenced by file map
                Int32 ct;
                Int32 lbn;
                while (p < q)
                {
                    if ((CTSZ == 1) && (LBSZ == 3)) // Format 1 (normal format)
                    {
                        lbn = H[p++] << 16;
                        ct = H[p++];
                        lbn += H.ToUInt16(ref p);
                    }
                    else if ((CTSZ == 2) && (LBSZ == 2)) // Format 2 (not implemented)
                    {
                        ct = H.ToUInt16(ref p);
                        lbn = H.ToUInt16(ref p);
                    }
                    else if ((CTSZ == 2) && (LBSZ == 4)) // Format3 (not implemented)
                    {
                        ct = H.ToUInt16(ref p);
                        lbn = H.ToUInt16(ref p) << 16;
                        lbn += H.ToUInt16(ref p);
                    }
                    else // unknown format
                    {
                        throw new InvalidDataException();
                    }
                    for (Int32 i = 0; i <= ct; i++)
                    {
                        mDisk[lbn + i].CopyTo(buf, bp);
                        bp += 512;
                    }
                }

                UInt16 nf = H.ToUInt16(map + 2);
                UInt16 ns = H.ToUInt16(map + 4);
                H = (nf == 0) ? null : GetFileHeader(nf, ns, 0);
            }
            return buf;
        }

        public override Boolean SaveFS(String fileName, String format)
        {
            throw new NotImplementedException();
        }

        private Block GetFileBlock(UInt16 fileNum, UInt16 seqNum, Int32 vbn)
        {
            return GetFileBlock(fileNum, seqNum, 0, vbn);
        }

        private Block GetFileBlock(UInt16 fileNum, UInt16 seqNum, UInt16 volNum, Int32 vbn)
        {
            if (fileNum == 0) throw new ArgumentOutOfRangeException("fileNum");
            if (volNum != 0) throw new ArgumentOutOfRangeException("volNum");
            if (vbn <= 0) throw new ArgumentOutOfRangeException("vbn");

            // get file header
            Block H = GetFileHeader(fileNum, seqNum, volNum);
            while (H != null)
            {
                Int32 map = H[1] * 2; // map area pointer
                Int32 CTSZ = H[map + 6];
                Int32 LBSZ = H[map + 7];
                Int32 q = map + H[map + 8] * 2 + 10; // end of retrieval pointers
                Int32 p = map + 10; // start of retrieval pointers

                // identify location of block in file map
                Int32 ct;
                Int32 lbn;
                while (p < q)
                {
                    if ((CTSZ == 1) && (LBSZ == 3)) // Format 1 (normal format)
                    {
                        lbn = H[p++] << 16;
                        ct = H[p++];
                        lbn += H.ToUInt16(ref p);
                    }
                    else if ((CTSZ == 2) && (LBSZ == 2)) // Format 2 (not implemented)
                    {
                        ct = H.ToUInt16(ref p);
                        lbn = H.ToUInt16(ref p);
                    }
                    else if ((CTSZ == 2) && (LBSZ == 4)) // Format3 (not implemented)
                    {
                        ct = H.ToUInt16(ref p);
                        lbn = H.ToUInt16(ref p) << 16;
                        lbn += H.ToUInt16(ref p);
                    }
                    else // unknown format
                    {
                        throw new InvalidDataException();
                    }
                    ct++;
                    if (vbn <= ct) return mDisk[lbn + vbn - 1];
                    vbn -= ct;
                }

                // if block wasn't found in this header, fetch next extension header
                UInt16 nf = H.ToUInt16(map + 2);
                UInt16 ns = H.ToUInt16(map + 4);
                H = (nf == 0) ? null : GetFileHeader(nf, ns, 0);
            }
            return null;
        }

        private Block GetFileHeader(UInt16 fileNum, UInt16 seqNum)
        {
            return GetFileHeader(fileNum, seqNum, 0);
        }

        private Block GetFileHeader(UInt16 fileNum, UInt16 seqNum, UInt16 volNum)
        {
            if (fileNum == 0) throw new ArgumentOutOfRangeException("fileNum");
            if (volNum != 0) throw new ArgumentOutOfRangeException("volNum");

            Block H = mDisk[1]; // home block
            Int32 IBSZ = H.ToUInt16(0);
            if (fileNum <= 16)
            {
                // first 16 file headers follow index bitmap (at LBN H.IBLB + H.IBSZ)
                H = mDisk[(H.ToUInt16(2) << 16) + H.ToUInt16(4) + IBSZ + fileNum - 1];
            }
            else
            {
                // must read index file for remaining file headers
                // desired header is at index file VBN H.IBSZ + 2 + fileNum
                H = GetFileBlock(1, 1, 0, IBSZ + 2 + fileNum);
            }

            if ((H.ToUInt16(2) != fileNum) || (H.ToUInt16(4) != seqNum)) return null;
            return H;
        }
    }
}
