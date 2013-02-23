﻿using MediaBrowser.Common.IO;
using MediaBrowser.Common.Kernel;
using MediaBrowser.Controller;
using MediaBrowser.Model.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MediaBrowser.Api.WebSocket
{
    /// <summary>
    /// Class ScheduledTasksWebSocketListener
    /// </summary>
    public class LogFileWebSocketListener : BasePeriodicWebSocketListener<IEnumerable<string>, LogFileWebSocketState>
    {
        /// <summary>
        /// Gets the name.
        /// </summary>
        /// <value>The name.</value>
        protected override string Name
        {
            get { return "LogFile"; }
        }

        /// <summary>
        /// The _kernel
        /// </summary>
        private readonly IKernel _kernel;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="LogFileWebSocketListener" /> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="kernel">The kernel.</param>
        public LogFileWebSocketListener(ILogger logger, Kernel kernel)
            : base(logger)
        {
            _kernel = kernel;
            _kernel.LoggerLoaded += kernel_LoggerLoaded;
        }

        /// <summary>
        /// Gets the data to send.
        /// </summary>
        /// <param name="state">The state.</param>
        /// <returns>IEnumerable{System.String}.</returns>
        protected override async Task<IEnumerable<string>> GetDataToSend(LogFileWebSocketState state)
        {
            if (!string.Equals(_kernel.LogFilePath, state.LastLogFilePath))
            {
                state.LastLogFilePath = _kernel.LogFilePath;
                state.StartLine = 0;
            }

            var lines = await GetLogLines(state.LastLogFilePath, state.StartLine).ConfigureAwait(false);

            state.StartLine += lines.Count;

            return lines;
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="dispose"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected override void Dispose(bool dispose)
        {
            if (dispose)
            {
                _kernel.LoggerLoaded -= kernel_LoggerLoaded;
            }
            base.Dispose(dispose);
        }

        /// <summary>
        /// Handles the LoggerLoaded event of the kernel control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        void kernel_LoggerLoaded(object sender, EventArgs e)
        {
            // Reset the startline for each connection whenever the logger reloads
            lock (ActiveConnections)
            {
                foreach (var connection in ActiveConnections)
                {
                    connection.Item4.StartLine = 0;
                }
            }
        }

        /// <summary>
        /// Gets the log lines.
        /// </summary>
        /// <param name="logFilePath">The log file path.</param>
        /// <param name="startLine">The start line.</param>
        /// <returns>Task{IEnumerable{System.String}}.</returns>
        internal static async Task<List<string>> GetLogLines(string logFilePath, int startLine)
        {
            var lines = new List<string>();

            using (var fs = new FileStream(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, StreamDefaults.DefaultFileStreamBufferSize, true))
            {
                using (var reader = new StreamReader(fs))
                {
                    while (!reader.EndOfStream)
                    {
                        lines.Add(await reader.ReadLineAsync().ConfigureAwait(false));
                    }
                }
            }

            if (startLine > 0)
            {
                lines = lines.Skip(startLine).ToList();
            }

            return lines;
        }
    }

    /// <summary>
    /// Class LogFileWebSocketState
    /// </summary>
    public class LogFileWebSocketState
    {
        /// <summary>
        /// Gets or sets the last log file path.
        /// </summary>
        /// <value>The last log file path.</value>
        public string LastLogFilePath { get; set; }
        /// <summary>
        /// Gets or sets the start line.
        /// </summary>
        /// <value>The start line.</value>
        public int StartLine { get; set; }
    }
}