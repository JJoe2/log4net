using log4net.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace log4net.Appender
{
    /// <summary>
    /// Provides data for the <see cref="IInternalLogger.Log"/> event.
    /// </summary>
    public class InternalLogEventArgs : EventArgs
    {

        /// <summary>
        /// Initializes a new instance of the <see cref="InternalLogEventArgs"/> class with a message and no exception.
        /// </summary>
        /// <param name="level">The <see cref="Level"/> at which to log this message.</param>
        /// <param name="message">The message to log.</param>
        public InternalLogEventArgs(Level level, string message) : this(level, message, null)
        {

        }

        /// <summary>
        /// Initializes a new instance of the <see cref="InternalLogEventArgs"/> class with a message and an exception.
        /// </summary>
        /// <param name="level">The <see cref="Level"/> at which to log this message.</param>
        /// <param name="message">The message to log.</param>
        /// <param name="exception">The optional <see cref="Exception"/> to log.</param>
        public InternalLogEventArgs(Level level, string message, Exception exception)
        {
            Level = level ?? (exception == null ? Level.Info : Level.Error);
            Message = message;
            Exception = exception;
        }

        /// <summary>
        /// Gets the <see cref="Level"/> at which to log.
        /// </summary>
        public Level Level { get; private set; }

        /// <summary>
        /// Gets the message to be logged.
        /// </summary>
        public string Message { get; private set; }

        /// <summary>
        /// Gets the exception to be logged.
        /// </summary>
        public Exception Exception { get; private set;}
    }
}
