using log4net.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace log4net.Appender
{
    /// <summary>
    /// Default implementation of <see cref="IAppendErrorHandler"/>, which handles errors by retrying a configurable number of times.
    /// </summary>
    /// <seealso cref="log4net.Appender.IAppendErrorHandler" />
    /// <seealso cref="log4net.Appender.IInternalLogger" />
    public class DefaultAppendErrorHandler : IAppendErrorHandler, IInternalLogger
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultAppendErrorHandler"/> class.
        /// </summary>
        public DefaultAppendErrorHandler()
        {
            RetryDelay = 100;
        }

        /// <summary>
        /// The delay between retries in milliseconds, or a negative number to disable retries.
        /// </summary>
        /// <remarks>
        /// The default value is 100 milliseconds.
        /// </remarks>
        public int RetryDelay { get; set; }

        /// <summary>
        /// The maximum number of retries.
        /// </summary>
        /// <remarks>
        /// The default value is 0, which disables retrying.
        /// </remarks>
        public int MaximumRetries { get; set; }

        private bool m_previousAppendFailed;

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
        /// <exception cref="System.ArgumentNullException">formattedLoggingEvents</exception>
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
        public int AppendFailed(IList<object> formattedLoggingEvents, Exception exception, int retryCount, TimeSpan elapsedTimeSinceFirstTry)
        {
            if (formattedLoggingEvents == null) throw new ArgumentNullException("formattedLoggingEvents");

            if ((retryCount > MaximumRetries) || (RetryDelay < 0))
            {
                string message = String.Format(CultureInfo.CurrentCulture, "Discarded {0} logging events because Append failed.", formattedLoggingEvents.Count);
                OnLog(new InternalLogEventArgs(Level.Error, message, exception));
                m_previousAppendFailed = true;
                return -1;
            }
            else
            {
                return RetryDelay;
            }
        }

        /// <summary>
        /// Called by <see cref="AsyncAppenderSkeleton" /> after a successful call to the <see cref="O:AsyncAppenderSkeleton.Append" />
        /// or <see cref="AsyncAppenderSkeleton.AppendFormattedEvents" /> method.
        /// </summary>
        /// <param name="formattedLoggingEvents">The formatted logging events that were being processed.</param>
        /// <remarks>
        /// This method could be used to reset a "once-only" error logger.
        /// </remarks>
        public void AppendSucceeded(IList<object> formattedLoggingEvents)
        {
            if (m_previousAppendFailed)
            {
                string message = String.Format(CultureInfo.CurrentCulture, "Successfully appended {0} logging events.", formattedLoggingEvents.Count);
                OnLog(new InternalLogEventArgs(Level.Error, message));
                m_previousAppendFailed = false;
            }
        }

        /// <summary>
        /// Raises the <see cref="E:Log" /> event.
        /// </summary>
        /// <param name="e">The <see cref="InternalLogEventArgs"/> instance containing the event data.</param>
        protected void OnLog(InternalLogEventArgs e)
        {
            if (e == null) throw new ArgumentNullException("e");

            EventHandler<InternalLogEventArgs> log = Log;
            if (log != null) log(this, e);
        }

        /// <summary>
        /// Occurs when a component wants to log a message.
        /// </summary>
        public event EventHandler<InternalLogEventArgs> Log;
    }
}
