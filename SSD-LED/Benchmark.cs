using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SSD_LED
{
    class Benchmark
    {


    }

    //FILE_FLAG_NO_BUFFERING is your friend... + https://stackoverflow.com/questions/5916673/how-to-do-non-cached-file-writes-in-c-sharp-winform-app
    /*
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern SafeFileHandle CreateFile(string lpFileName, [MarshalAs(UnmanagedType.U4)] FileAccess dwDesiredAccess, [MarshalAs(UnmanagedType.U4)] FileShare dwShareMode, IntPtr lpSecurityAttributes, [MarshalAs(UnmanagedType.U4)] FileMode dwCreationDisposition, [MarshalAs(UnmanagedType.U4)] FileAttributes dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        FileOptions fileOptions = (FileOptions)(134217728 | (int.MinValue ) | (536870912));
        SafeFileHandle file = CreateFile(f, FileAccess.Read, FileShare.ReadWrite, IntPtr.Zero, FileMode.Open, (FileAttributes)fileOptions, IntPtr.Zero);
        if (file.IsInvalid)
            return;
        FileStream fileStream = new FileStream(file, FileAccess.Read, 4096, false);
    */

    /*
    //from MS verve/singularity C# OS - base/Windows/Benchmarks/fileops/fileops.cs
    //see also https://github.com/jgarbacz/ArkadinMigration/blob/c205b1f090656645f513aca79b5acf6f2dc935ea/MvmSourceCodeMercurial/MVM_DLL/BufferedFileSystem/UnbufferedFileLoader.cs
    class FileReader
    {
        //const uint FILE_FLAG_NO_BUFFERING = 0x20000000;
        const uint FILE_FLAG_NO_BUFFERING = 0;
        const uint GENERIC_READ = 0x80000000;
        const uint OPEN_EXISTING = 3;
        IntPtr handle;

        [DllImport("kernel32", SetLastError = true)]
        private static extern unsafe IntPtr CreateFile(
            string FileName,                    // file name
            uint DesiredAccess,                 // access mode
            uint ShareMode,                     // share mode
            uint SecurityAttributes,            // Security Attributes
            uint CreationDisposition,           // how to create
            uint FlagsAndAttributes,            // file attributes
            int hTemplateFile                   // handle to template file
            );

        [DllImport("kernel32", SetLastError = true)]
        private static extern unsafe bool ReadFile(
            IntPtr hFile,                       // handle to file
            void* pBuffer,                      // data buffer
            int NumberOfBytesToRead,            // number of bytes to read
            int* pNumberOfBytesRead,            // number of bytes read
            int Overlapped                      // overlapped buffer
            );

        [DllImport("kernel32", SetLastError = true)]
        private static extern unsafe bool CloseHandle(
            IntPtr hObject                      // handle to object
            );

        [DllImport("kernel32", SetLastError = true)]
        private static extern unsafe uint GetFileSize(
            IntPtr hObject,                     // handle to object
            uint* pFileSizeHigh                 // receives high 32-bits of file size.
            );

        public bool Open(string FileName)
        {
            // open the existing file for reading
            handle = CreateFile(
                FileName,
                GENERIC_READ,
                0,
                0,
                OPEN_EXISTING,
                FILE_FLAG_NO_BUFFERING,
                0);
            if (handle != IntPtr.Zero && handle != ((IntPtr)(-1)))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public unsafe int Read(byte[] buffer, int index, int count)
        {
            int n = 0;
            fixed (byte* p = buffer)
            {
                if (!ReadFile(handle, p + index, count, &n, 0))
                {
                    return 0;
                }
            }
            return n;
        }

        public bool Close()
        {
            // close file handle
            return CloseHandle(handle);
        }

        public unsafe int Size()
        {
            return (int)GetFileSize(handle, null);
        }
    }

    public class SpecMain
    {
        //--------------------------------------------------
        // Main routine
        //--------------------------------------------------
        public static int Main(string[] args)
        {
            FileReader reader = new FileReader();

            DateTime start = DateTime.Now;

            for (int i = 1; i < 1000; i++)
            {
                if (!reader.Open(args[0]))
                {
                    Console.WriteLine("Failed");
                    throw new Exception("argh..");
                }
                reader.Close();
            }
            DateTime end = DateTime.Now;
            TimeSpan elapsed = end - start;

            Console.WriteLine("5000 iterations = {0}", elapsed);
            return 0;
        } // main
    } //specmain class}
    */

    /*
//maybe we can avoid win32 api except createfile - this is from https://github.com/bodhifan/BatchSend/blob/98154e80a337bbc968649feb220f556fb0b41e7a/CopyDirectory.cs
[DllImport("kernel32.dll", SetLastError = true)]
static extern SafeFileHandle CreateFile(string IpFileName, uint dwDesiredAccess,
        uint dwShareMode, IntPtr IpSecurityAttributes, uint dwCreationDisposition,
        uint dwFlagsAndAttributes, IntPtr hTemplateFile);
public void copyFileStream(string sourceFile, string destFile)
{
    //  progressFrm.SetSubProgressMax(100);
    bool useBuffer = false;
    SafeFileHandle fr = CreateFile(sourceFile, GENERIC_READ, FILE_SHARE_READ, IntPtr.Zero, OPEN_EXISTING, useBuffer ? 0 : FILE_FLAG_NO_BUFFERING, IntPtr.Zero);
    SafeFileHandle fw = CreateFile(destFile, GENERIC_WRITE, FILE_SHARE_READ, IntPtr.Zero, CREATE_ALWAYS, FILE_FLAG_NORMAL, IntPtr.Zero);

    int bufferSize = useBuffer ? 1024 * 1024 * 32 : 1024 * 1024 * 32;

    FileStream fsr = new FileStream(fr, FileAccess.Read, bufferSize, false);
    FileStream fsw = new FileStream(fw, FileAccess.Write, bufferSize, false);

    BinaryReader br = new BinaryReader(fsr);
    BinaryWriter bw = new BinaryWriter(fsw);

    byte[] buffer = new byte[bufferSize];
    Int64 len = fsr.Length;
    DateTime start = DateTime.Now;
    TimeSpan ts;
    while (fsr.Position < fsr.Length)
    {
        int readCount = br.Read(buffer, 0, bufferSize);
        bw.Write(buffer, 0, readCount);
        ts = DateTime.Now.Subtract(start);
        double speed = (double)fsr.Position / ts.TotalMilliseconds * 1000 / (1024 * 1024);
        double progress = (double)fsr.Position / len * 100;
        //     progressFrm.SetSubLabel("当前拷贝文件：" + sourceFile + " 速度：" + speed);
        //      progressFrm.SetSubProgress((int)progress);
    }
    br.Close();
    bw.Close();
}
*/
}
