using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace log4net.Appender
{
    /// <summary>
    /// Interface implemented by error handlers used by <see cref="IAppenderQueue"/> implementations to define the
    /// error handling policy.
    /// </summary>
    public interface IAppendErrorHandler
    {
        /// <summary>
        /// Called by an <see cref="IAppenderQueue" /> implementation after a call to the <see cref="O:AsyncAppenderSkeleton.Append" /> 
        /// or <see cref="AsyncAppenderSkeleton.AppendFormattedEvents" /> method threw an exception.
        /// </summary>
        /// <param name="formattedLoggingEvents">The formatted logging events that were being processed.</param>
        /// <param name="exception">The <see cref="Exception" /> that was thrown.</param>
        /// <param name="retryCount">The number of failed attempts so far.</param>
        /// <param name="elapsedTimeSinceFirstTry">The elapsed time since the first try to send these logging events.</param>
        /// <returns>
        /// the number of milliseconds to wait before retrying the call to <see cref="O:AsyncAppenderSkeleton.Append" />,
        /// or a negative number to abandon retrying.
        /// </returns>
        /// <remarks>
        /// Implementations will typically:
        /// <list type="bullet"><item>
        /// Inspect the <paramref name="exception" /> parameter to determine if the error is permanent or transient.  For example, an appender that
        /// sends events to a SOAP web service might consider a SOAP fault with a Sender fault code to be permanent, and a SOAP fault with a Receiver
        /// fault code to be transient.
        /// </item><item>
        /// Inspect the <paramref name="retryCount" /> parameter to limit the number of retries before discarding the logging events.
        /// </item><item>
        /// Inspect the <paramref name="elapsedTimeSinceFirstTry" /> parameter to abandon retrying if too much time has elapsed.
        /// </item></list>
        /// </remarks>
        int AppendFailed(IList<object> formattedLoggingEvents, Exception exception, int retryCount, TimeSpan elapsedTimeSinceFirstTry);

        /// <summary>
        /// Called by <see cref="AsyncAppenderSkeleton"/> after a successful call to the <see cref="O:AsyncAppenderSkeleton.Append" /> 
        /// or <see cref="AsyncAppenderSkeleton.AppendFormattedEvents" /> method.
        /// </summary>
        /// <param name="formattedLoggingEvents">The formatted logging events that were being processed.</param>
        /// <remarks>
        /// This method could be used to reset a "once-only" error logger.
        /// </remarks>
        void AppendSucceeded(IList<object> formattedLoggingEvents);

    }
}
