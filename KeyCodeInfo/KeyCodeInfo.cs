using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Timers;
using Rainmeter;

namespace KeyCodeInfoPlugin
{
    internal class Measure
    {
        private API _api;
      
        private static IntPtr _hookID = IntPtr.Zero;
        private static LowLevelKeyboardProc _proc = HookCallback;
        private static Measure instance;
        private static int lastKeyCode = 0;
        private static KBDLLHOOKSTRUCT lastKeyEvent;
        private static Dictionary<int, KBDLLHOOKSTRUCT> pressedKeyEvents = new Dictionary<int, KBDLLHOOKSTRUCT>();
        private static List<int> pressedKeyOrder = new List<int>();
        private Timer updateTimer = null;
        private string measureName = "";
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYUP = 0x0105;

        // Parameters:
        // showCodeMode:
        //   1 = return decimal,
        //   3 = return hex (e.g., "0x4A"),
        //   0 = return friendly name,
        //   4 = return key combination (e.g., "Ctrl + Alt + A") in order of press.
        private int showCodeMode;
        // hideForce: for modes 0, 1, and 3, if true the stored key is cleared immediately.
        // For mode 4 (combination), keys remain until they are released.
        private bool hideForce;

        /// <summary>
        /// Reload is called when Rainmeter reloads the skin.
        /// It reads ShowCode and HideForce options and resets stored key data.
        /// (Note: the hook and update timer are not started automatically.)
        /// </summary>
        internal void Reload(API api, ref double maxValue)
        {
            _api = api;
            // Store the instance reference for use in the static hook callback.
            instance = this;

            // Read ShowCode parameter; default is 1 (numeric).
            // Other supported values: 3 for hex, 0 for friendly names, 4 for combination.
            showCodeMode = api.ReadInt("ShowCode", 1);
            // Read HideForce parameter; default is 1 (clear immediately).
            hideForce = api.ReadInt("HideForce", 1) == 1;

            // Reset stored key data.
            lastKeyCode = 0;
            pressedKeyEvents.Clear();
            pressedKeyOrder.Clear();

            _api.Log(API.LogType.Debug, "KeyCodeInfo.dll: Plugin reloaded in stopped state.");
        }

        /// <summary>
        /// Update returns the last captured key code (numeric value).
        /// </summary>
        internal double Update()
        {
            return lastKeyCode;
        }

        /// <summary>
        /// GetString returns the display string based on the mode:
        /// - Mode 1: returns the key code as a decimal string.
        /// - Mode 3: returns the key code as a hexadecimal string (e.g., "0x4A").
        /// - Mode 0: returns a friendly key name.
        /// - Mode 4: returns a key combination string in the order keys were pressed.
        /// If no key has been pressed, returns an empty string.
        /// For modes 0, 1, and 3, if hideForce is true the stored key is cleared.
        /// For mode 4, keys remain until released.
        /// </summary>
        internal string GetString()
        {
            if (showCodeMode == 4)
            {
                // Build combination string using press order.
                if (pressedKeyOrder.Count == 0)
                    return "";
                List<string> comboNames = new List<string>();
                foreach (int key in pressedKeyOrder)
                {
                    if (pressedKeyEvents.TryGetValue(key, out KBDLLHOOKSTRUCT keyStruct))
                    {
                        string keyName = GetKeyName(keyStruct);
                        if (string.IsNullOrEmpty(keyName) && key >= 32 && key <= 126)
                            keyName = ((char)key).ToString();
                        if (string.IsNullOrEmpty(keyName))
                            keyName = key.ToString();
                        comboNames.Add(keyName);
                    }
                }
                return string.Join(" + ", comboNames);
            }
            else
            {
                int code = lastKeyCode;
                if (code == 0)
                    return "";

                string result;
                if (showCodeMode == 1)
                    result = code.ToString();
                else if (showCodeMode == 3)
                    result = "0x" + code.ToString("X2");
                else // mode 0: friendly name
                {
                    result = GetKeyName(lastKeyEvent);
                    if (string.IsNullOrEmpty(result) && code >= 32 && code <= 126)
                        result = ((char)code).ToString();
                }
                if (hideForce)
                    lastKeyCode = 0;
                return result;
            }
        }

        /// <summary>
        /// GetKeyName uses the Windows API function GetKeyNameText to retrieve a friendly name for the key.
        /// </summary>
        private string GetKeyName(KBDLLHOOKSTRUCT keyStruct)
        {
            int lParam = (int)(keyStruct.scanCode << 16);
            if ((keyStruct.flags & 1) != 0)
                lParam |= (1 << 24);
            StringBuilder sb = new StringBuilder(64);
            int result = GetKeyNameText(lParam, sb, sb.Capacity);
            if (result > 0)
                return sb.ToString();
            return "";
        }

        /// <summary>
        /// ExecuteCommand processes bang commands to start or stop the plugin.
        /// When starting, it retrieves the measure name and starts a timer that manually updates the measure.
        /// </summary>
        internal void ExecuteCommand(string command)
        {
            if (string.IsNullOrEmpty(command))
            {
                _api.Log(API.LogType.Debug, "KeyCodeInfo.dll: No command provided.");
                return;
            }
            if (command.Equals("Start", StringComparison.InvariantCultureIgnoreCase))
            {
                // Start the keyboard hook if not running.
                if (_hookID == IntPtr.Zero)
                {
                    _hookID = SetHook(_proc);
                    _api.Log(API.LogType.Debug, "KeyCodeInfo.dll: Keyboard hook started via command.");
                }
                else
                {
                    _api.Log(API.LogType.Debug, "KeyCodeInfo.dll: Keyboard hook already running.");
                }
                // Retrieve the measure name.
                measureName = _api.GetMeasureName();
                _api.Log(API.LogType.Debug, "KeyCodeInfo.dll: Measure name = " + measureName);
                // Start the update timer if not already running.
                if (updateTimer == null)
                {
                    updateTimer = new Timer(50); // Timer interval in milliseconds.
                    updateTimer.Elapsed += (sender, e) =>
                    {
                        _api.Execute($"!UpdateMeasure  \"{measureName}\"");
                        _api.Execute($"!UpdateMeter  *");
                        _api.Execute($"!Redraw");
                        // _api.Execute($"!Log  \"Updating : {measureName}\"");
                    };
                    updateTimer.AutoReset = true;
                    updateTimer.Start();
                    _api.Log(API.LogType.Debug, "KeyCodeInfo.dll: Update timer started.");
                }
            }
            else if (command.Equals("Stop", StringComparison.InvariantCultureIgnoreCase))
            {
                // Stop the keyboard hook if running.
                if (_hookID != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(_hookID);
                    _api.Log(API.LogType.Debug, "KeyCodeInfo.dll: Keyboard hook stopped via command.");
                    _hookID = IntPtr.Zero;
                }
                else
                {
                    _api.Log(API.LogType.Debug, "KeyCodeInfo.dll: Keyboard hook is not running.");
                }
                // Stop the update timer.
                if (updateTimer != null)
                {
                    updateTimer.Stop();
                    updateTimer.Dispose();
                    updateTimer = null;
                    _api.Log(API.LogType.Debug, "KeyCodeInfo.dll: Update timer stopped.");
                }
            }
            else
            {
                _api.Log(API.LogType.Debug, $"KeyCodeInfo.dll: Unknown command: {command}");
            }
        }

        /// <summary>
        /// Unload is called when the plugin is unloaded.
        /// It removes the keyboard hook and stops the update timer.
        /// </summary>
        internal void Unload()
        {
            if (_hookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookID);
                _api.Log(API.LogType.Debug, "KeyCodeInfo.dll: Keyboard hook removed on Unload.");
                _hookID = IntPtr.Zero;
            }
            if (updateTimer != null)
            {
                updateTimer.Stop();
                updateTimer.Dispose();
                updateTimer = null;
                _api.Log(API.LogType.Debug, "KeyCodeInfo.dll: Update timer stopped on Unload.");
            }
        }

        // Sets up the low-level keyboard hook.
        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                IntPtr moduleHandle = GetModuleHandle(curModule.ModuleName);
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, moduleHandle, 0);
            }
        }

        // Callback for processing keyboard events.
        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                // Handle key-down events.
                if (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN)
                {
                    KBDLLHOOKSTRUCT kbStruct = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));
                    int key = (int)kbStruct.vkCode;
                    lastKeyCode = key;
                    lastKeyEvent = kbStruct;
                    // If the active instance is in combination mode, update tracking.
                    if (instance != null && instance.showCodeMode == 4)
                    {
                        if (pressedKeyEvents.ContainsKey(key))
                        {
                            // Remove the key from the order list so it can be re-added at the end.
                            pressedKeyOrder.Remove(key);
                        }
                        pressedKeyEvents[key] = kbStruct;
                        pressedKeyOrder.Add(key);
                    }
                }
                // Handle key-up events.
                else if (wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP)
                {
                    KBDLLHOOKSTRUCT kbStruct = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));
                    int key = (int)kbStruct.vkCode;
                    if (instance != null && instance.showCodeMode == 4)
                    {
                        if (pressedKeyEvents.ContainsKey(key))
                        {
                            pressedKeyEvents.Remove(key);
                        }
                        pressedKeyOrder.Remove(key);
                    }
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        #region P/Invoke Definitions

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetKeyNameText(int lParam, [Out] StringBuilder lpString, int nSize);

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        #endregion
    }

    public static class Plugin
    {
        // For GetString: store pointer to allocated string to free later.
        private static IntPtr lastStringPtr = IntPtr.Zero;

        [DllExport]
        public static void Initialize(ref IntPtr data, IntPtr rm)
        {
            data = GCHandle.ToIntPtr(GCHandle.Alloc(new Measure()));
        }

        [DllExport]
        public static void Finalize(IntPtr data)
        {
            Measure measure = (Measure)GCHandle.FromIntPtr(data).Target;
            measure.Unload();
            GCHandle.FromIntPtr(data).Free();
            if (lastStringPtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(lastStringPtr);
                lastStringPtr = IntPtr.Zero;
            }
        }

        [DllExport]
        public static void Reload(IntPtr data, IntPtr rm, ref double maxValue)
        {
            Measure measure = (Measure)GCHandle.FromIntPtr(data).Target;
            measure.Reload(new API(rm), ref maxValue);
        }

        [DllExport]
        public static double Update(IntPtr data)
        {
            Measure measure = (Measure)GCHandle.FromIntPtr(data).Target;
            return measure.Update();
        }

        [DllExport]
        public static IntPtr GetString(IntPtr data)
        {
            Measure measure = (Measure)GCHandle.FromIntPtr(data).Target;
            string s = measure.GetString();
            if (lastStringPtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(lastStringPtr);
                lastStringPtr = IntPtr.Zero;
            }
            lastStringPtr = Marshal.StringToHGlobalUni(s);
            return lastStringPtr;
        }

        [DllExport]
        public static void ExecuteBang(IntPtr data, [MarshalAs(UnmanagedType.LPWStr)] string args)
        {
            Measure measure = (Measure)GCHandle.FromIntPtr(data).Target;
            measure.ExecuteCommand(args);
        }
    }
}
