using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace log4net.Appender
{
    /// <summary>
    /// Provides data for the <see cref="IAppenderQueue.ItemsDequeued"/> event.
    /// </summary>
    public class DequeuedEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DequeuedEventArgs"/> class.
        /// </summary>
        /// <param name="formattedLoggingEvents"></param>
        public DequeuedEventArgs(IList<object> formattedLoggingEvents)
        {
            FormattedLoggingEvents = formattedLoggingEvents;
        }

        /// <summary>
        /// Gets an enumeration of formatted logging events that have been dequeued.
        /// </summary>
        public IList<object> FormattedLoggingEvents { get; private set; }
    }
}
