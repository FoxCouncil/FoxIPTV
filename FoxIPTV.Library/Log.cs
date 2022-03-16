// Copyright (c) 2019 Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Library
{
    using Utils;
    using System;
    using System.IO;
    using System.Reflection;
    using System.Diagnostics;

    public static class Log
    {
        /// <summary>The default filename for the applications logfile</summary>
        private const string Filename = "FoxIPTV.log";

        /// <summary>The synchronizer object for writing to the logfile</summary>
        private static readonly object _logWriterLock = new object();

        /// <summary>The stream writer for writing to the logfile</summary>
        private static StreamWriter _logWriter;

        internal static string FilePath
        {
            set
            {
                lock (_logWriterLock)
                {
                    if (_logWriter == null)
                    {
                        _logWriter = new StreamWriter(Path.Combine(value, Filename), append: true);
                    }
                }
            }
        }

        /// <summary>A in-memory log storage, limited to 1,000 items</summary>
        private static readonly FixedQueue<string> _logBuffer = new FixedQueue<string> { FixedSize = 1000 };

#if DEBUG
        /// <summary>During debug, all logging is turned on</summary>
        public static LogLevel Level { get; set; } = LogLevel.All;
#else
        /// <summary>Release builds only report errors</summary>
        public static LogLevel Level { get; set; } = Level.Error;
#endif

        /// <summary>A shortcut method for sending a log message of type Error</summary>
        /// <param name="message">The Error message needed to be logged</param>
        public static void Error(string message) => InternalLog(LogLevel.Error, message);

        /// <summary>A shortcut method for sending a log message of type Info</summary>
        /// <param name="message">The Info message needed to be logged</param>
        public static void Info(string message) => InternalLog(LogLevel.Info, message);

        /// <summary>A shortcut method for sending a log message of type Debug</summary>
        /// <param name="message">The Debug message needed to be logged</param>
        public static void Debug(string message) => InternalLog(LogLevel.Debug, message);

        /// <summary>A shortcut method for sending a basic log message</summary>
        /// <param name="message">The message needed to be logged</param>
        public static void Message(string message) => InternalLog(LogLevel.Message, message);

        /// <summary>Log a message of a certain type</summary>
        /// <param name="logLevel">The type of log this is</param>
        /// <param name="message">The log message</param>
        private static void InternalLog(LogLevel logLevel, string message)
        {
            if (logLevel == LogLevel.None || logLevel > Level)
            {
                return;
            }

            lock (_logWriterLock)
            {
                var logLine = $"[{DateTime.UtcNow:O}]-[{logLevel.ToString().ToUpper().PadLeft(7)}]: {message}";

                _logWriter.WriteLine(logLine);
                _logWriter.Flush();

                _logBuffer.Enqueue(logLine);

                if (Debugger.IsAttached)
                {
                    Debugger.Log(0, logLevel.ToString().ToUpper(), $"[FoxIPTV][{logLevel.ToString().ToUpper().PadLeft(7)}]: {message}");
                }
            }
        }
    }
}
