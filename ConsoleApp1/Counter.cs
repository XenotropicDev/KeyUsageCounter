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
        private static LowLevelKeyboardProc _proc = HookCallback;
        private static IntPtr _hookID = IntPtr.Zero;

        private static List<KeyCount> PressedKeyCounts;
        private static string LoggingDir = "C:\\temp\\";
        private static string FileName = "key_counts.txt";

        public static string ListHeaders = "Key,KeyUpCount,KeyDownCount";

        private static System.Timers.Timer SaveTimer = new System.Timers.Timer(30000);

        public static List<int> ActiveKeyMods = new List<int>();

        public static void Main()
        {
            // Set up the program
            PressedKeyCounts = new List<KeyCount>();
            if (!Directory.Exists(LoggingDir))
            {
                Directory.CreateDirectory(LoggingDir);
            }

            // Load from existing file
            if (File.Exists(LoggingDir + FileName))
            {
                try
                {
                    using (var reader = new StreamReader(File.OpenRead(LoggingDir + FileName)))
                    {
                        while (!reader.EndOfStream)
                        {
                            string line = reader.ReadLine();
                            var itemToAdd = new KeyCount(line.Split(','));
                            PressedKeyCounts.Add(itemToAdd);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    Console.ReadKey();
                    return; // Stop code execution
                }
            }

            // Start the save timer
            SaveTimer.Elapsed += SaveTimer_Elapsed;
            SaveTimer.AutoReset = true;
            SaveTimer.Enabled = true;

            // Run the main function
            _hookID = SetHook(_proc);
            Application.Run();
            UnhookWindowsHookEx(_hookID);
        }

        private static void SaveTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            SaveListToFile();
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
            if (nCode >= 0)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                var keyPressed = (Keys)vkCode;

                if (wParam.IsKeyDown())
                {
                    // Check if the active keys need to change
                    if (!ActiveKeyMods.Contains(vkCode))
                    {
                        ActiveKeyMods.Add(vkCode);
                    }
                }

                if (wParam.IsKeyUp())
                {
                    ActiveKeyMods.Remove(vkCode);
                    //SaveListToFile();
                }

                KeyPressToList(keyPressed, wParam.IsKeyUp());
                DisplayKey();
                //DisplayStats();
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private static void KeyPressToList(Keys key, bool isKeyUp)
        {
            var selectedKey = PressedKeyCounts.FirstOrDefault(k => k.Key.Equals(key));

            // Create the key counter if it doesn't exist
            if (selectedKey == default(KeyCount))
            {
                selectedKey = new KeyCount(key);
                PressedKeyCounts.Add(selectedKey);
            }
            if (isKeyUp) selectedKey.KeyUpCounter++; else selectedKey.KeyDownCounter++;
        }

        private static void DisplayStats()
        {
            Console.Clear();
            var worklist = PressedKeyCounts.OrderByDescending(k => k.KeyUpCounter).Take(Console.WindowHeight);
            foreach (var kc in worklist)
            {
                Console.WriteLine($"{kc.Key.ToString()} : {kc.KeyUpCounter}");
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
            try
            {
                using (TextWriter tw = new StreamWriter(LoggingDir + FileName, false))
                {
                    tw.WriteLine(ListHeaders);
                    foreach (var kc in PressedKeyCounts)
                        tw.WriteLine($"{kc.Key.ToString()},{kc.KeyUpCounter},{kc.KeyDownCounter}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"File could not be saved: {ex.Message}");
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
        public int KeyUpCounter { get; set; }
        public int KeyDownCounter { get; set; }

        public static List<int> KeyMods { get; set; }

        public KeyCount()
        {
            Key = default(Keys);
            KeyUpCounter = 0;
            KeyDownCounter = 0;
        }

        public KeyCount(Keys key)
        {
            Key = key;
            KeyUpCounter = 0;
            KeyUpCounter = 0;
        }

        public KeyCount(Keys key, int upCount, int downCount)
        {
            Key = key;
            KeyUpCounter = upCount;
            KeyDownCounter = downCount;
        }

        public KeyCount(string[] input)
        {
            try
            {
                this.Key = (Keys)new KeysConverter().ConvertFromString(input[0]);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Couldn't import '{input[0]}' from string array. {ex.Message}");
                return;
            }
            int temp;
            if (Int32.TryParse(input[1], out temp)) this.KeyUpCounter = temp;
            if (Int32.TryParse(input[2], out temp)) this.KeyDownCounter = temp;
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