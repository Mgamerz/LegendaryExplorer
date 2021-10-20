using System;
using System.Collections.Generic;
using System.Diagnostics;
using LegendaryExplorer.GameInterop.InteropTargets;
using NamedPipeWrapper;

namespace LegendaryExplorer.GameInterop.ConsoleCommandExecutors
{
    /// <summary>
    /// Executes console commands, and sends and receives messages from an interop ASI via named pipes
    /// </summary>
    public class InteropNamedPipeClient : IDisposable, IConsoleCommandExecutor
    {
        private readonly InteropTarget target;
        private readonly NamedPipeClient<string> client;

        public InteropNamedPipeClient(InteropTarget tgt, string pipeName)
        {
            target = tgt;
            client = new NamedPipeClient<string>(pipeName);
            client.ServerMessage += OnClientMessage;
            client.Error += OnError;
            client.Start(); // Do we want to do this in constructor? is there an easy way to lazily start client when we actually need it?
        }

        public void ExecuteConsoleCommands(IEnumerable<string> commands)
        {
            foreach (var cmd in commands)
            {
                SendMessage($"ce:{cmd}");
            }
        }

        public void SendMessage(string message)
        {
            client.PushMessage(message);
        }

        private void OnClientMessage(NamedPipeConnection<string, string> conn, string message)
        {
            target.RaiseReceivedMessage(message);
        }

        private void OnError(Exception exception)
        {
            Debug.WriteLine($"Named Pipe Error ({target.Game}): {exception}");
        }

        public void Dispose()
        {
            client.Stop();
        }
    }
}