using log4net.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace log4net.Appender
{
    /// <summary>
    /// Base class for <see cref="IAppenderQueue"/> implementations
    /// </summary>
    /// <seealso cref="log4net.Appender.IAppenderQueue" />
    /// <seealso cref="log4net.Appender.IInternalLogger" />
    public class AppenderQueueSkeleton : IAppenderQueue, IInternalLogger
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AppenderQueueSkeleton"/> class.
        /// </summary>
        public AppenderQueueSkeleton()
        {
            CurrentQueueLength = -1;
            ErrorHandler = new DefaultAppendErrorHandler();
        }

        /// <summary>
        /// Gets or sets the <see cref="IAppendErrorHandler"/> used by this queue to handler append errors.
        /// </summary>
        public IAppendErrorHandler ErrorHandler { get; set; }

        /// <summary>
        /// Fires when formatted logging event items are dequeued.
        /// </summary>
        public event EventHandler<DequeuedEventArgs> ItemsDequeued;


        /// <summary>
        /// Gets a value indicating whether the last attempt to append logging events failed.
        /// </summary>
        protected bool LastAppendFailed { get; private set; }

        /// <summary>
        /// Raises the <see cref="E:ItemsDequeued"/> event.
        /// </summary>
        /// <param name="e">The <see cref="DequeuedEventArgs"/> instance containing the event data.</param>
        protected void OnItemsDequeued(DequeuedEventArgs e)
        {
            if (e == null) throw new ArgumentNullException("e");

            EventHandler<DequeuedEventArgs> itemsDequeued = ItemsDequeued;
            if (itemsDequeued == null) return;

            Stopwatch stopwatch = Stopwatch.StartNew();
            int retries = 0;
            
            for (; ; )
            {
                try
                {
                    itemsDequeued(this, e);
                    LastAppendFailed = false;
                    if (ErrorHandler != null) ErrorHandler.AppendSucceeded(e.FormattedLoggingEvents);
                    return;
                }
                catch(Exception ex)
                {
                    retries++;
                    LastAppendFailed = true;
                    if (ErrorHandler == null) return;
                    int retryDelay = ErrorHandler.AppendFailed(e.FormattedLoggingEvents, ex, retries, stopwatch.Elapsed);
                    if (retryDelay < 0) return;
                    // TODO: will probably change this to a wait on an event so it can be terminated when the queue is disposed.
                    Thread.Sleep(retryDelay);
                }
            }
        }

        /// <summary>
        /// Fires when the queue has been flushed, to enable the appender to do its own flush.
        /// </summary>
        public event EventHandler<FlushedEventArgs> Flushed;

        /// <summary>
        /// Raises the <see cref="E:Flushed" /> event.
        /// </summary>
        /// <param name="e">The <see cref="FlushedEventArgs"/> instance containing the event data.</param>
        /// <exception cref="System.ArgumentNullException">e</exception>
        protected void OnFlushed(FlushedEventArgs e)
        {
            if (e == null) throw new ArgumentNullException("e");

            EventHandler<FlushedEventArgs> flushed = Flushed;
            if (flushed != null) flushed(this, e);
        }

        /// <summary>
        /// Enqueues a formetted logging event.
        /// </summary>
        /// <param name="formattedLoggingEvent">The formatted logging event.</param>
        /// <returns>
        ///   <c>True</c> if the logging event was enqueued successfully; <c>false</c> if the queue was full.
        /// </returns>
        /// <exception cref="System.NotImplementedException"></exception>
        /// <remarks>
        /// An appender derived from <see cref="AsyncAppenderSkeleton" /> can optionally override <see cref="AsyncAppenderSkeleton.FormatLoggingEvent" />
        /// to generate a formatted logging event that can be queued for asynchronous processing.  If this method is not overridden, the
        /// formatted logging event is the original <see cref="LoggingEvent" /> with fields fixed according to the <see cref="AsyncAppenderSkeleton.Fix" />
        /// property.
        /// </remarks>
        public bool Enqueue(object formattedLoggingEvent)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Indicates that the queue may start dequeuing events.
        /// </summary>
        /// <exception cref="System.NotImplementedException"></exception>
        /// <remarks>
        /// This is intended for an implementation that uses a persistent queue, where there may be events in the
        /// queue at application startup.  Such a queue should not start
        /// dequeueing events immediately in case log4net is not yet completely configured.  Start will be called
        /// when AsyncAppenderSkeleton.DoAppend is called for the first time: only then can we be sure that log4net is configured.
        /// </remarks>
        protected virtual void Start()
        {
        }

        /// <summary>
        /// Handles the Log event of the ErrorHandler component.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="InternalLogEventArgs"/> instance containing the event data.</param>
        private void ErrorHandler_Log(object sender, InternalLogEventArgs e)
        {
            // Forward to the appender
            OnLog(e);
        }

        /// <summary>
        /// Indicates that the queue may start dequeuing events.
        /// </summary>
        /// <remarks>
        /// This is intended for an implementation that uses a persistent queue, where there may be events in the
        /// queue at application startup.  Such a queue should not start
        /// dequeueing events immediately in case log4net is not yet completely configured.  Start will be called
        /// when AsyncAppenderSkeleton.DoAppend is called for the first time: only then can we be sure that log4net is configured.
        /// </remarks>
        void IAppenderQueue.Start()
        {
            this.Start();
        }


        /// <summary>
        /// Gets the current length of the queue if the implementation supports it, else -1.
        /// </summary>
        public int CurrentQueueLength
        {
            get;
            protected set;
        }

        /// <summary>
        /// Flushes any buffered log data.
        /// </summary>
        /// <param name="millisecondsTimeout">The maximum time to wait for logging events to be flushed.</param>
        /// <returns>
        ///   <c>True</c> if all logging events were flushed successfully, else <c>false</c>.
        /// </returns>
        /// <exception cref="System.NotImplementedException"></exception>
        /// <remarks>
        /// Appenders that implement the <see cref="Flush" /> method must do so in a thread-safe manner: it can be called concurrently with
        /// the <see cref="log4net.Appender.IAppender.DoAppend" /> method.
        /// <para>
        /// Typically this is done by locking on the Appender instance, e.g.:
        /// <code><![CDATA[
        /// public bool Flush(int millisecondsTimeout)
        /// {
        /// lock(this)
        /// {
        /// // Flush buffered logging data
        /// ...
        /// }
        /// }
        /// ]]></code></para><para>
        /// The <paramref name="millisecondsTimeout" /> parameter is only relevant for appenders that process logging events asynchronously,
        /// such as <see cref="RemotingAppender" />.
        /// </para>
        /// </remarks>
        public virtual bool Flush(int millisecondsTimeout)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Activate the options that were previously set with calls to properties.
        /// </summary>
        /// <exception cref="System.NotImplementedException"></exception>
        /// <remarks>
        /// <para>
        /// This allows an object to defer activation of its options until all
        /// options have been set. This is required for components which have
        /// related options that remain ambiguous until all are set.
        /// </para>
        /// <para>
        /// If a component implements this interface then this method must be called
        /// after its properties have been set before the component can be used.
        /// </para>
        /// </remarks>
        protected void ActivateOptions()
        {
        }

        /// <summary>
        /// Activate the options that were previously set with calls to properties.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This allows an object to defer activation of its options until all
        /// options have been set. This is required for components which have
        /// related options that remain ambiguous until all are set.
        /// </para>
        /// <para>
        /// If a component implements this interface then this method must be called
        /// after its properties have been set before the component can be used.
        /// </para>
        /// </remarks>
        void IOptionHandler.ActivateOptions()
        {
            IInternalLogger loggingErrorHandler = ErrorHandler as IInternalLogger;
            if (loggingErrorHandler != null) loggingErrorHandler.Log += ErrorHandler_Log;

            this.ActivateOptions();
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);

            // SuppressFinalize for the unlikely event that a derived class implements a finalizer.
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
        }

        /// <summary>
        /// Occurs when a component wants to log a message.
        /// </summary>
        public event EventHandler<InternalLogEventArgs> Log;

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
    }
}
