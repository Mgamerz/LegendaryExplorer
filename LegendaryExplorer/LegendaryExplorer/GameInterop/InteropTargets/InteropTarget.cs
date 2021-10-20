using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LegendaryExplorer.GameInterop.ConsoleCommandExecutors;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorer.GameInterop.InteropTargets
{
    /// <summary>
    /// Abstract class representing a game that can be used for interop
    /// </summary>
    public abstract class InteropTarget
    {
        public event Action<string> GameReceiveMessage;
        public abstract MEGame Game { get; }
        protected abstract IConsoleCommandExecutor ConsoleCommandExecutor { get; }
        public abstract bool CanUpdateTOC { get; }
        public abstract string InteropASIName { get; }
        /// <summary>
        /// The file name of a deprecated ASI, if any.
        /// </summary>
        public virtual string OldInteropASIName => null;
        public abstract string InteropASIDownloadLink { get; }
        public abstract string InteropASIMD5 { get; }
        /// <summary>
        /// MD5 of Bink Bypass. Only required for OT games
        /// </summary>
        public abstract string BinkBypassMD5 { get; }
        public abstract string OriginalBinkMD5 { get; }
        public abstract InteropModInfo ModInfo { get; }
        public abstract string ProcessName { get; }
        public abstract uint GameMessageSignature { get; }

        public virtual bool TryGetProcess(out Process process)
        {
            process = Process.GetProcessesByName(ProcessName).FirstOrDefault();
            return process != null;
        }

        public void ExecuteConsoleCommands(params string[] commands) =>
            ExecuteConsoleCommands(commands.AsEnumerable());
        public void ExecuteConsoleCommands(IEnumerable<string> commands)
        {
            if (ConsoleCommandExecutor is not null)
            {
                ConsoleCommandExecutor.ExecuteConsoleCommands(commands);
            }
            else
            {
                Debug.WriteLine($"No console command executor assigned for game {Game}!");
            }
        }

        public bool IsGameInstalled() => MEDirectories.GetExecutablePath(Game) is string exePath && File.Exists(exePath);

        public abstract void SelectGamePath();

        internal void RaiseReceivedMessage(string message)
        {
            if (GameReceiveMessage != null) GameReceiveMessage.Invoke(message);
        }
    }
}