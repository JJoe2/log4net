using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace log4net.Appender
{
    /// <summary>
    /// Interface that enables components to do their internal logging by firing an event.
    /// </summary>
    /// <remarks>
    /// This is mainly intended for use by components such as <see cref="IAppenderQueue"/> implementations so that
    /// they can route internal logging through the appender they belong to.
    /// </remarks>
    public interface IInternalLogger
    {
        /// <summary>
        /// Occurs when a component wants to log a message.
        /// </summary>
        event EventHandler<InternalLogEventArgs> Log;
    }
}
