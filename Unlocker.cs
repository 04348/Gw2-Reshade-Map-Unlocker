using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Gw2GraphicalOverall
{
    /*
        Reading and writing memory to unlock reshade

        http://www.codeproject.com/Articles/670373/Csharp-Read-Write-another-Process-Memory helped a lot to convert the program from C++ to C#

    */
    class Unlocker
    {
        // in order : WSASend, WSASendTo, WSARecv, WSARecvEx, WSARecvFrom, send, sendto, recv, recvfrom(?)
        static int[] address = { 0xA3A62, 0xA3AF2, 0xA3BEE, 0xA3C93, 0xA3D9D, 0xA3E40, 0xA3F10, 0xA4028, 0xA4117 }; //offset of target assembly code
        static int[] wordSize = {8, 8, 8, 8, 8, 9, 9, 8, 9}; //Size of the assembly commands
        static int[] normalValue = { 0xF0 }; // F0 = xadd
        static byte[] newValue = { 0x90 }; // 90 = nop

        const int maxTry = 20; //number of time it will look for Gw2, before closing if it wasn't found
        const int timeBetweenTry = 1000;//in ms

        // const used if memory reading/writing command
        const int PROCESS_WM_READ = 0x0010;
        const int PROCESS_VM_WRITE = 0x0020;
        const int PROCESS_VM_OPERATION = 0x0008;

        public static void Execute()
        {
            Int64 D3D9BaseAddress = 0;
            Process[] processlist;
            Process Gw2Process = null;

            Console.WriteLine("Waiting for Gw2 process...");

            {
                int i = 0;
                while (D3D9BaseAddress == 0 && ++i<=maxTry)
                {
                    processlist = Process.GetProcesses();
                    foreach (Process theprocess in processlist)
                    {
                        if (theprocess.ProcessName.Contains("Gw2-64"))
                        {
                            Console.WriteLine("Process: {0} ID: {1} ADR:{2}", theprocess.ProcessName, theprocess.Id, theprocess.MainModule.BaseAddress.ToInt64());
                            Gw2Process = theprocess;
                            foreach (ProcessModule module in Gw2Process.Modules)
                            {
                                if (module.FileName.Contains("d3d9.dll") && !module.FileName.Contains("system"))
                                {
                                    Console.WriteLine("Module: {0} Base Address : {1:X}", module.FileName, module.BaseAddress.ToInt64());
                                    D3D9BaseAddress = module.BaseAddress.ToInt64();
                                }
                            }
                        }
                    }
                    Thread.Sleep(timeBetweenTry);
                }

                if (D3D9BaseAddress == 0)
                {
                    Console.WriteLine("Reshade not found");
                    Environment.Exit(0);   //If Gw2 or Reshade are not found, exit
                }
            }

            
            if (CheckMemory(Gw2Process, D3D9BaseAddress))   //If values at given addresses are correct
            {
                Console.WriteLine("Writing...");
                WriteMemory(Gw2Process, D3D9BaseAddress); // nop them all !
            }
            /*
             TODO : Do we need to exit if Reshade can't be unlocked, or we still write mumble link ?
            */
        }

        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);


        [DllImport("kernel32.dll")]
        public static extern bool ReadProcessMemory(int hProcess, Int64 lpBaseAddress, ref Byte lpBuffer, int dwSize, ref int lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool WriteProcessMemory(int hProcess, Int64 lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesWritten);

        /*
            Return true if all the target addresses are xadd
            Used to check if it's not used with another version of reshade, game will crash otherwise
        */
        private static bool CheckMemory(Process proc, Int64 BaseAddress)
        {
            IntPtr processHandle = OpenProcess(PROCESS_WM_READ, false, proc.Id);
            byte val = 0;
            int byteRead = 0;
            for (int i = 0; i < address.Length; i++)
            {
                Console.Write("@{0:X} : ", BaseAddress + address[i]);
                for (int j = 0; j < normalValue.Length; j++)
                {
                   ReadProcessMemory((int)processHandle, BaseAddress + address[i] + j, ref val, 1, ref byteRead);
                    Console.Write("{0:X}.", val);
                    if (val != normalValue[j]) return false;
                }
                Console.WriteLine();
            }
            return true;
        }

        /*
            Replace all bytes of the xadds commands with nop
        */
        private static void WriteMemory(Process proc, Int64 BaseAddress)
        {
            IntPtr processHandle = OpenProcess(PROCESS_VM_WRITE | PROCESS_VM_OPERATION, false, proc.Id);
            int byteWritten = 0;
            for (int i = 0; i < address.Length; i++)
            {
                for (int j = 0; j < wordSize[i]; j++)
                {
                    WriteProcessMemory((int)processHandle, BaseAddress + address[i] + j, newValue, 1, ref byteWritten);
                }
            }
        }
    }
    
}
