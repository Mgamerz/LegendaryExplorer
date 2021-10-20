using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using LegendaryExplorer.GameInterop.InteropTargets;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Packages;
using Keys = System.Windows.Forms.Keys;

namespace LegendaryExplorer.GameInterop
{
    /// <summary>
    /// Handles receipt of WndProc messages from Interop ASI mods, and stores the InteropTargets
    /// </summary>
    public static class GameController
    {
        // TODO: Split up WndProc hook and InteropTarget management into two different classes
        private static readonly Dictionary<MEGame, InteropTarget> Targets = new()
        {
            {MEGame.LE1, new LE1InteropTarget()},
            {MEGame.ME2, new ME2InteropTarget()},
            {MEGame.ME3, new ME3InteropTarget()}
        };

        public static InteropTarget GetInteropTargetForGame(MEGame game)
        {
            if (Targets.ContainsKey(game)) return Targets[game];
            return null;
        }

        public static bool TryGetMEProcess(MEGame game, out Process meProcess)
        {
            meProcess = null;
            return GetInteropTargetForGame(game)?.TryGetProcess(out meProcess) ?? false;
        }

        public static bool SendME3TOCUpdateMessage()
        {
            return ((ME3InteropTarget)Targets[MEGame.ME3]).SendTOCUpdateMessage();
        }

        #region WndProc Message Hook
        private static bool hasRegisteredForMessages; 
        public static void InitializeMessageHook(Window window)
        {
            if (hasRegisteredForMessages) return;
            hasRegisteredForMessages = true;
            if (PresentationSource.FromVisual(window) is HwndSource hwndSource)
            {
                hwndSource.AddHook(WndProc);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        struct COPYDATASTRUCT
        {
            public ulong dwData;
            public uint cbData;
            public IntPtr lpData;
        }

        private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_COPYDATA = 0x004a;
            // const uint SENT_FROM_ME3 = 0x02AC00C2;
            // const uint SENT_FROM_ME2 = 0x02AC00C3;
            // const uint SENT_FROM_ME1 = 0x02AC00C4;
            // const uint SENT_FROM_LE3 = 0x02AC00C5;
            // const uint SENT_FROM_LE2 = 0x02AC00C6;
            // const uint SENT_FROM_LE1 = 0x02AC00C7;
            if (msg == WM_COPYDATA)
            {
                COPYDATASTRUCT cds = Marshal.PtrToStructure<COPYDATASTRUCT>(lParam);
                foreach (var target in Targets.Values)
                {
                    if (cds.dwData == target.GameMessageSignature)
                    {
                        string value = Marshal.PtrToStringUni(cds.lpData);
                        handled = true;
                        target.RaiseReceivedMessage(value);
                        return (IntPtr)1;
                    }
                }
            }
            return IntPtr.Zero;
        }

        [DllImport("user32.dll")]
        private static extern bool SendMessage(IntPtr hWnd, uint Msg, int wParam, int lParam);

        internal static bool SendTOCMessage(IntPtr hWnd, uint Msg)
        {
            return SendMessage(hWnd, Msg, 0, 0);
        }

        #endregion
    }
}
