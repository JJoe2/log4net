using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace log4net.Appender
{
    /// <summary>
    /// Provides data for the <see cref="IAppenderQueue.Flushed"/> event.
    /// </summary>
    public class FlushedEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FlushedEventArgs"/> class.
        /// </summary>
        /// <param name="millisecondsTimeout"></param>
        public FlushedEventArgs(int millisecondsTimeout)
        {
            MillisecondsTimeout = millisecondsTimeout < 0 ? 0 : millisecondsTimeout;
        }

        /// <summary>
        /// Gets the timeout to be used by event handlers for their flush implementation.
        /// </summary>
        public int MillisecondsTimeout { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating if an event handler failed to flush logging data.
        /// </summary>
        /// <remarks>
        /// Defaults to <c>false</c>.  An event handler sets it to <c>true</c> if it failed to
        /// completely flush all logging data.
        /// </remarks>
        public bool FlushFailed { get; set; }
    }
}
