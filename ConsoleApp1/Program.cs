namespace KeyUsageCounter
{
    using System;
    using System.Diagnostics;
    using System.Windows.Forms;
    using System.Runtime.InteropServices;
    using System.Collections.Generic;
    using System.Linq;
    using System.IO;

    class InterceptKeys
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYUP = 0x0101; //Down keys are one HEX less for each
        private const int WM_SYSKEYUP = 0x0105;
        private static LowLevelKeyboardProc _proc = HookCallback;
        private static IntPtr _hookID = IntPtr.Zero;

        private static List<KeyCount> KeyCounts;
        private static string LoggingDir = @"C:\temp";

        private static List<int> ActiveKeyMods = new List<int>();

        public static void Main()
        {
            // Set up the program
            KeyCounts = new List<KeyCount>();
            if (!Directory.Exists(LoggingDir))
            {
                Directory.CreateDirectory(LoggingDir);
            }

            // Run the main function
            _hookID = SetHook(_proc);
            Application.Run();
            UnhookWindowsHookEx(_hookID);
        }

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0) // && (wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP))
            {
                int vkCode = Marshal.ReadInt32(lParam);
                if (wParam.IsKeyDown())
                {
                    if (!ActiveKeyMods.Contains(vkCode))
                    {
                        ActiveKeyMods.Add(vkCode);
                        DisplayKey();
                    }
                }

                if (wParam.IsKeyUp())
                {
                    var keyup = (Keys)vkCode;

                    ActiveKeyMods.Remove(vkCode);

                    KeyPressToList(keyup);
                    //DisplayStats();
                    DisplayKey();
                    SaveListToFile();                    
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private static void KeyPressToList(Keys key)
        {
            // Create the key counter if it doesn't exist
            if (KeyCounts.FirstOrDefault(k => k.Key.Equals(key)) == default(KeyCount))
            {
                KeyCounts.Add(new KeyCount(key, 0));
            }

            var selectedKey = KeyCounts.FirstOrDefault(k => k.Key.Equals(key));

            if (!selectedKey.Key.Equals(default(Keys)))
            {
                selectedKey.Counter++;
            }
        }

        private static void DisplayStats()
        {
            Console.Clear();
            var worklist = KeyCounts.OrderByDescending(k => k.Counter).Take(Console.WindowHeight);
            foreach (var kc in worklist)
            {
                Console.WriteLine($"{kc.Key.ToString()} : {kc.Counter}");
            }
        }

        private static void DisplayKey()
        {
            Console.Clear();
            String s = String.Join(" + ", ActiveKeyMods.ToArray());
            Console.WriteLine(s);
        }

        private static void SaveListToFile()
        {
            using (TextWriter tw = new StreamWriter(@"c:\temp\key_counts.txt", false))
            {
                foreach (var kc in KeyCounts)
                    tw.WriteLine($"{kc.Key.ToString()},{kc.Counter}");
            }
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook,
            LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,
            IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

    }

    internal class KeyCount
    {
        public Keys Key { get; set; }
        public int Counter { get; set; }

        public KeyCount()
        {
            Key = default(Keys);
            Counter = 0;
        }

        public KeyCount(Keys key)
        {
            Key = key;
            Counter = 0;
        }

        public KeyCount(Keys key, int count)
        {
            Key = key;
            Counter = count;
        }
    }

    public static class ExtensionMethods
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100; 
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_KEYUP = 0x0101; 
        private const int WM_SYSKEYUP = 0x0105;

        public static bool IsKeyDown(this IntPtr ip)
        {
            return (ip == (IntPtr)WM_KEYDOWN || ip == (IntPtr)WM_SYSKEYDOWN);
        }

        public static bool IsKeyUp(this IntPtr ip)
        {
            return (ip == (IntPtr)WM_KEYUP || ip == (IntPtr)WM_SYSKEYUP);
        }

        public static bool IsSysKey(this IntPtr ip)
        {
            return (ip == (IntPtr)WM_SYSKEYDOWN || ip == (IntPtr)WM_SYSKEYUP);
        }

        public static bool IsModKey(this int vkCode)
        {
            int[] mods = { 0x10, 0x11, 0x12, 0x29,
                0x5B, 0x5C, 0x5D, 0x90, 
                0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5, };

            return (mods.Contains(vkCode));
        }
    }
}