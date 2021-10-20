using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using LegendaryExplorer.GameInterop.InteropTargets;
using LegendaryExplorerCore.GameFilesystem;

namespace LegendaryExplorer.GameInterop.ConsoleCommandExecutors
{
    /// <summary>
    /// Writes console commands to an exec file and sends keystrokes to the game to execute them
    /// </summary>
    public class ConsoleCommandDirectExec : IConsoleCommandExecutor
    {
        private readonly InteropTarget target;
        private const string ExecFileName = "lexinterop";
        public ConsoleCommandDirectExec(InteropTarget tgt)
        {
            target = tgt;
        }

        public void ExecuteConsoleCommands(IEnumerable<string> commands)
        {
            if (target.TryGetProcess(out var gameProcess))
            {
                ExecuteConsoleCommands(gameProcess.MainWindowHandle, commands);
            }
        }

        private void ExecuteConsoleCommands(IntPtr hWnd, IEnumerable<string> commands)
        {
            string execFilePath = Path.Combine(MEDirectories.GetDefaultGamePath(target.Game), "Binaries", ExecFileName);

            File.WriteAllText(execFilePath, string.Join(Environment.NewLine, commands));
            DirectExecuteConsoleCommand(hWnd, $"exec {ExecFileName}");
        }

        /// <summary>
        /// Executes a console command on the game whose window handle is passed.
        /// <param name="command"/> can ONLY contain [a-z0-9 ]
        /// </summary>
        /// <param name="gameWindowHandle"></param>
        /// <param name="command"></param>
        private static void DirectExecuteConsoleCommand(IntPtr gameWindowHandle, string command)
        {
            SendKey(gameWindowHandle, Keys.Tab);
            foreach (char c in command)
            {
                if (characterMapping.TryGetValue(c, out Keys key))
                {
                    SendKey(gameWindowHandle, key);
                }
                else
                {
                    throw new ArgumentException("Invalid characters!", nameof(command));
                }
            }
            SendKey(gameWindowHandle, Keys.Enter);
        }

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, int wParam, int lParam);
        private const int WM_SYSKEYDOWN = 0x0104;

        private static void SendKey(IntPtr hWnd, Keys key) => SendKey(hWnd, (int)key);
        private static void SendKey(IntPtr hWnd, int key) => PostMessage(hWnd, WM_SYSKEYDOWN, key, 0);

        private static readonly Dictionary<char, Keys> characterMapping = new()
        {
            ['a'] = Keys.A,
            ['b'] = Keys.B,
            ['c'] = Keys.C,
            ['d'] = Keys.D,
            ['e'] = Keys.E,
            ['f'] = Keys.F,
            ['g'] = Keys.G,
            ['h'] = Keys.H,
            ['i'] = Keys.I,
            ['j'] = Keys.J,
            ['k'] = Keys.K,
            ['l'] = Keys.L,
            ['m'] = Keys.M,
            ['n'] = Keys.N,
            ['o'] = Keys.O,
            ['p'] = Keys.P,
            ['q'] = Keys.Q,
            ['r'] = Keys.R,
            ['s'] = Keys.S,
            ['t'] = Keys.T,
            ['u'] = Keys.U,
            ['v'] = Keys.V,
            ['w'] = Keys.W,
            ['x'] = Keys.X,
            ['y'] = Keys.Y,
            ['z'] = Keys.Z,
            ['0'] = Keys.D0,
            ['1'] = Keys.D1,
            ['2'] = Keys.D2,
            ['3'] = Keys.D3,
            ['4'] = Keys.D4,
            ['5'] = Keys.D5,
            ['6'] = Keys.D6,
            ['7'] = Keys.D7,
            ['8'] = Keys.D8,
            ['9'] = Keys.D9,
            [' '] = Keys.Space,
        };
    }
}