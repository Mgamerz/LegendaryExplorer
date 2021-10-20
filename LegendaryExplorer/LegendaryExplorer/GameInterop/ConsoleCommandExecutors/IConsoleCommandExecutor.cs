using System.Collections.Generic;

namespace LegendaryExplorer.GameInterop.ConsoleCommandExecutors
{
    public interface IConsoleCommandExecutor
    {
        public void ExecuteConsoleCommands(IEnumerable<string> commands);
    }
}