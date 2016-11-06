using log4net.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace log4net.Appender
{
    /// <summary>
    /// Interface for a queue used by <see cref="AsyncAppenderSkeleton"/>.
    /// </summary>
    /// <remarks>
    /// Any initialization required should be performed in the <see cref="IOptionHandler.ActivateOptions"/> method.
    /// <para>
    /// Any cleanup should be performed in <see cref="IDisposable.Dispose"/>
    /// </para>
    /// </remarks>
    public interface IAppenderQueue: IFlushable, IOptionHandler, IDisposable
    {
        /// <summary>
        /// Fires when formatted logging event items are dequeued.
        /// </summary>
        event EventHandler<DequeuedEventArgs> ItemsDequeued;

        /// <summary>
        /// Fires when the queue has been flushed, to enable the appender to do its own flush.
        /// </summary>
        event EventHandler<FlushedEventArgs> Flushed;

        /// <summary>
        /// Enqueues a formetted logging event.
        /// </summary>
        /// <param name="formattedLoggingEvent">The formatted logging event.</param>
        /// <returns><c>True</c> if the logging event was enqueued successfully; <c>false</c> if the queue was full.</returns>
        /// <remarks>
        /// An appender derived from <see cref="AsyncAppenderSkeleton"/> can optionally override <see cref="AsyncAppenderSkeleton.FormatLoggingEvent"/>
        /// to generate a formatted logging event that can be queued for asynchronous processing.  If this method is not overridden, the
        /// formatted logging event is the original <see cref="LoggingEvent"/> with fields fixed according to the <see cref="AsyncAppenderSkeleton.Fix"/>
        /// property.
        /// </remarks>
        bool Enqueue(object formattedLoggingEvent);

        /// <summary>
        /// Indicates that the queue may start dequeuing events.
        /// </summary>
        /// <remarks>
        /// This is intended for an implementation that uses a persistent queue, where there may be events in the
        /// queue at application startup.  Such a queue should not start
        /// dequeueing events immediately in case log4net is not yet completely configured.  Start will be called
        /// when AsyncAppenderSkeleton.DoAppend is called for the first time: only then can we be sure that log4net is configured.
        /// </remarks>
        void Start();

        /// <summary>
        /// Gets the current length of the queue if the implementation supports it, else -1.
        /// </summary>
        int CurrentQueueLength { get; }

    }
}
