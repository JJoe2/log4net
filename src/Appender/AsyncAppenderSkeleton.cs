#region Apache License
//
// Licensed to the Apache Software Foundation (ASF) under one or more 
// contributor license agreements. See the NOTICE file distributed with
// this work for additional information regarding copyright ownership. 
// The ASF licenses this file to you under the Apache License, Version 2.0
// (the "License"); you may not use this file except in compliance with 
// the License. You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
#endregion

using System;
using System.IO;
using System.Collections;
using System.Linq;

using log4net.Filter;
using log4net.Util;
using log4net.Layout;
using log4net.Core;
using System.Threading;
using System.Collections.Generic;

namespace log4net.Appender
{
    /// <summary>
    /// Abstract base class implementation of <see cref="IAppender"/>. 
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class provides the code for common functionality, such 
    /// as support for threshold filtering and support for general filters.
    /// </para>
    /// <para>
    /// The implementation is based on <see cref="AppenderSkeleton"/>, with the addition of support for optional
    /// asynchronous appending of events.
    /// </para>
    /// </remarks>
    public abstract class AsyncAppenderSkeleton : AppenderBase, IAppender, IBulkAppender, IOptionHandler, IFlushable, IDisposable
    {
		#region Protected Instance Constructors

		/// <summary>
		/// Default constructor
		/// </summary>
		/// <remarks>
		/// <para>Empty default constructor</para>
		/// </remarks>
		protected AsyncAppenderSkeleton()
		{
            Fix = FixFlags.Partial;
            // TODO: Queue = ... default queue implementation
		}

		#endregion Protected Instance Constructors

		#region Public Instance Properties

        /// <summary>
        /// Gets or sets a value indicating whether this appender is operating in asynchronous mode.
        /// </summary>
        public bool IsAsynchronous
        {
            get { return m_isAsynchronous; }
            set
            {
                if (IsConfigured) throw new InvalidOperationException("Cannot change IsAsynchronous property after appender has been configured.");
                m_isAsynchronous = value;
            }
        }
        private bool m_isAsynchronous;

        /// <summary>
        /// Gets or sets the <see cref="IAppenderQueue"/> implementation that manages queueing when <see cref="IsAsynchronous"/> is <c>true</c>.
        /// </summary>
        public IAppenderQueue Queue
        {
            get { return m_queue; }
            set
            {
                if (IsConfigured) throw new InvalidOperationException("Cannot change Queue property after appender has been configured.");
                m_queue = value;
            }
        }
        private IAppenderQueue m_queue;
        

		/// <summary>
		/// Gets or sets a the fields that will be fixed in the <see cref="LoggingEvent"/>
        /// when it is queued.
		/// </summary>
		/// <value>
		/// The event fields that will be fixed before the event is queued.
		/// </value>
		/// <remarks>
		/// <para>
		/// The logging event needs to have certain thread specific values 
		/// captured before it can be queued. See <see cref="LoggingEvent.Fix"/>
		/// for details.
		/// </para>
		/// </remarks>
		/// <seealso cref="LoggingEvent.Fix"/>
		virtual public FixFlags Fix {get; set;}

		#endregion

		#region Implementation of IOptionHandler

		/// <summary>
		/// Initialize the appender based on the options set
		/// </summary>
		/// <remarks>
		/// <para>
		/// This is part of the <see cref="IOptionHandler"/> delayed object
		/// activation scheme. The <see cref="ActivateOptions"/> method must 
		/// be called on this object after the configuration properties have
		/// been set. Until <see cref="ActivateOptions"/> is called this
		/// object is in an undefined state and must not be used. 
		/// </para>
		/// <para>
		/// If any of the configuration properties are modified then 
		/// <see cref="ActivateOptions"/> must be called again.
		/// </para>
		/// </remarks>
		protected virtual void ActivateOptions() 
		{
		}

        /// <summary>
        /// Implemented explicitly so we 
        /// </summary>
        void IOptionHandler.ActivateOptions()
        {
            try
            {
                if (IsConfigured) throw new InvalidOperationException("ActivateOptions may only be called once.");
                if (IsAsynchronous)
                {
                    if (Queue == null) throw new InvalidOperationException("Appender is asynchronous but has no queue.");
                    Queue.ActivateOptions();
                    Queue.ItemsDequeued += Queue_ItemsDequeued;
                    Queue.Flushed += Queue_Flushed;
                }
                // Configure derived class
                this.ActivateOptions();
                IsConfigured = true;
                IsEnabled = true;
            }
            catch(Exception ex)
            {
                string message = String.Format("ActivateOptions failed, appender {0} disabled.", Name);
                ErrorHandler.Error(message, ex);
                // this appender instance will ignore logging events because IsEnabled is false
            }
        }

        void Queue_Flushed(object sender, FlushedEventArgs e)
        {
            if (e.FlushFailed) return;

            if (!this.Flush(e.MillisecondsTimeout))
            {
                e.FlushFailed = true;
            }
        }

        /// <summary>
        /// Exceptions from derived classes will be propagated to the Queue implementation, which must implement retry  and error handling logic.
        /// </summary>
        /// <param name="sender">The object that raised this event</param>
        /// <param name="e">The <see cref="DequeuedEventArgs"/>.</param>
        void Queue_ItemsDequeued(object sender, DequeuedEventArgs e)
        {
            // TODO: review locking.  We should not call the Append method in a derived class from more than one thread concurrently
            // Also we need to protect against Close being called concurrently with Append

            IList<LoggingEvent> loggingEvents = GetAsLoggingEvents(e.FormattedLoggingEvents);
            if (loggingEvents != null)
            {
                Append(loggingEvents.ToArray());
            }
            else
            {
                AppendFormattedEvents(e.FormattedLoggingEvents);
            }
        }

        /// <summary>
        /// Gets or sets a value indicating if this appender is currently enabled and able to process logging events.
        /// <para>
        /// It is set to <c>true</c> when <see cref="ActivateOptions"/> has completed successfully, and to <c>false</c>
        /// when <see cref="Close"/> is called.
        /// </para>
        /// </summary>
        protected bool IsEnabled { get; set; }

		#endregion Implementation of IOptionHandler

		#region Implementation of IAppender

		/// <summary>
		/// Closes the appender and release resources.
		/// </summary>
		/// <remarks>
		/// <para>
		/// Release any resources allocated within the appender such as file handles, 
		/// network connections, etc.
		/// </para>
		/// <para>
		/// It is a programming error to append to a closed appender.
		/// </para>
		/// <para>
		/// This method cannot be overridden by subclasses. This method 
		/// delegates the closing of the appender to the <see cref="AppenderBase.OnClose"/>
		/// method which must be overridden in the subclass.
		/// </para>
		/// </remarks>
		public void Close()
		{
            // TODO: review locking and queue flushing.  Should Dispose flush?  If so what timeout?

			// This lock prevents the appender being closed while it is still appending
			lock(this)
			{
				if (!m_closed)
				{
                    if (Queue != null) Queue.Dispose();
					OnClose();
					m_closed = true;
				}
			}
		}

        // TODO: XML Documentation for DoAppend to be updated

        /// <overloads>Sends logging events to the logging sink.</overloads>
		/// <summary>
		/// Performs threshold checks and invokes filters before 
		/// delegating actual logging to the subclasses specific 
		/// <see cref="M:Append(LoggingEvent)"/> method.
		/// </summary>
		/// <param name="loggingEvent">The event to log.</param>
		/// <remarks>
		/// <para>
		/// This method cannot be overridden by derived classes. A
		/// derived class should override the <see cref="M:Append(LoggingEvent)"/> method
		/// which is called by this method.
		/// </para>
		/// <para>
		/// The implementation of this method is as follows:
		/// </para>
		/// <para>
		/// <list type="bullet">
		///		<item>
		///			<description>
		///			Checks that the severity of the <paramref name="loggingEvent"/>
		///			is greater than or equal to the <see cref="AppenderBase.Threshold"/> of this
		///			appender.</description>
		///		</item>
		///		<item>
		///			<description>
		///			Checks that the <see cref="IFilter"/> chain accepts the 
		///			<paramref name="loggingEvent"/>.
		///			</description>
		///		</item>
		///		<item>
		///			<description>
		///			Calls <see cref="M:PreAppendCheck()"/> and checks that 
		///			it returns <c>true</c>.</description>
		///		</item>
		/// </list>
		/// </para>
		/// <para>
		/// If all of the above steps succeed then the <paramref name="loggingEvent"/>
		/// will be passed to the abstract <see cref="M:Append(LoggingEvent)"/> method.
		/// </para>
		/// </remarks>
		public void DoAppend(LoggingEvent loggingEvent) 
		{

            // TODO: review locking.

			// This lock is absolutely critical for correct formatting
			// of the message in a multi-threaded environment.  Without
			// this, the message may be broken up into elements from
			// multiple thread contexts (like get the wrong thread ID).


			lock(this)
			{
                EnsureQueueStarted();

                if (!IsConfigured) return;
                if (!IsEnabled) return;
                if (m_closed)
				{
					ErrorHandler.Error("Attempted to append to closed appender named ["+Name+"].");
					return;
				}

				// prevent re-entry
				if (m_recursiveGuard)
				{
					return;
				}

				try
				{
					m_recursiveGuard = true;

					if (FilterEvent(loggingEvent) && PreAppendCheck())
					{
                        object formattedLoggingEvent = FormatLoggingEvent(loggingEvent);
                        loggingEvent = formattedLoggingEvent as LoggingEvent;

                        if (IsAsynchronous)
                        {
                            // If after formatting we have a logging event, it needs to be fixed.
                            if (loggingEvent != null) loggingEvent.Fix = Fix;

                            if (!Queue.Enqueue(formattedLoggingEvent))
                            {
                                // Queue overflowed
                                // TODO: error handling?
                                ErrorHandler.Error("AsyncAppender queue overflowed, logging event discarded");
                            }
                        }
                        else
                        {
                            if (loggingEvent != null)
                            {
                                this.Append(loggingEvent);
                            }
                            else
                            {
                                this.AppendFormattedEvents(new[] {formattedLoggingEvent});
                            }
                        }
					}
				}
				catch(Exception ex)
				{
					ErrorHandler.Error("Failed in DoAppend", ex);
				}
#if !MONO && !NET_2_0 && !NETSTANDARD1_3
				// on .NET 2.0 (and higher) and Mono (all profiles), 
				// exceptions that do not derive from System.Exception will be
				// wrapped in a RuntimeWrappedException by the runtime, and as
				// such will be catched by the catch clause above
				catch
				{
					// Catch handler for non System.Exception types
					ErrorHandler.Error("Failed in DoAppend (unknown exception)");
				}
#endif
				finally
				{
					m_recursiveGuard = false;
				}
			}
		}

		#endregion Implementation of IAppender

		#region Implementation of IBulkAppender

		/// <summary>
		/// Performs threshold checks and invokes filters before 
		/// delegating actual logging to the subclasses specific 
		/// <see cref="M:Append(LoggingEvent[])"/> method.
		/// </summary>
		/// <param name="loggingEvents">The array of events to log.</param>
		/// <remarks>
		/// <para>
		/// This method cannot be overridden by derived classes. A
		/// derived class should override the <see cref="M:Append(LoggingEvent[])"/> method
		/// which is called by this method.
		/// </para>
		/// <para>
		/// The implementation of this method is as follows:
		/// </para>
		/// <para>
		/// <list type="bullet">
		///		<item>
		///			<description>
		///			Checks that the severity of the <paramref name="loggingEvents"/>
        ///			is greater than or equal to the <see cref="AppenderBase.Threshold"/> of this
		///			appender.</description>
		///		</item>
		///		<item>
		///			<description>
		///			Checks that the <see cref="IFilter"/> chain accepts the 
		///			<paramref name="loggingEvents"/>.
		///			</description>
		///		</item>
		///		<item>
		///			<description>
		///			Calls <see cref="M:PreAppendCheck()"/> and checks that 
		///			it returns <c>true</c>.</description>
		///		</item>
		/// </list>
		/// </para>
		/// <para>
		/// If all of the above steps succeed then the <paramref name="loggingEvents"/>
		/// will be passed to the <see cref="M:Append(LoggingEvent[])"/> method.
		/// </para>
		/// </remarks>
		public void DoAppend(LoggingEvent[] loggingEvents) 
		{

            // TODO: review locking.

			// This lock is absolutely critical for correct formatting
			// of the message in a multi-threaded environment.  Without
			// this, the message may be broken up into elements from
			// multiple thread contexts (like get the wrong thread ID).

			lock(this)
			{
                EnsureQueueStarted();

                if (!IsConfigured) return;
                if (!IsEnabled) return;
                if (m_closed)
				{
					ErrorHandler.Error("Attempted to append to closed appender named ["+Name+"].");
					return;
				}

				// prevent re-entry
				if (m_recursiveGuard)
				{
					return;
				}

				try
				{
					m_recursiveGuard = true;

                    List<LoggingEvent> filteredEvents = new List<LoggingEvent>(loggingEvents.Length);

					foreach(LoggingEvent loggingEvent in loggingEvents)
					{
						if (FilterEvent(loggingEvent))
						{
							filteredEvents.Add(loggingEvent);
						}
					}

                    var formattedLoggingEvents = FormatLoggingEvents(filteredEvents);

                    if (formattedLoggingEvents.Count > 0 && PreAppendCheck())
					{
                        if (IsAsynchronous)
                        {
                            foreach(object formattedLoggingEvent in formattedLoggingEvents)
                            {
                                LoggingEvent loggingEvent = formattedLoggingEvent as LoggingEvent;

                                // If after formatting we have a logging event, it needs to be fixed.
                                if (loggingEvent != null) loggingEvent.Fix = Fix;

                                if (!Queue.Enqueue(formattedLoggingEvent))
                                {
                                    // Queue overflowed
                                    // TODO: error handling?
                                    ErrorHandler.Error("AsyncAppender queue overflowed, logging event discarded");
                                }

                            }
                        }
                        else
                        {
                            loggingEvents = GetAsLoggingEvents(formattedLoggingEvents).ToArray();
                            if (loggingEvents != null)
                            {
                                this.Append(loggingEvents);
                            }
                            else
                            {
                                this.AppendFormattedEvents(formattedLoggingEvents);
                            }
                        }
					}
				}
				catch(Exception ex)
				{
					ErrorHandler.Error("Failed in Bulk DoAppend", ex);
				}
#if !MONO && !NET_2_0 && !NETSTANDARD1_3
				// on .NET 2.0 (and higher) and Mono (all profiles), 
				// exceptions that do not derive from System.Exception will be
				// wrapped in a RuntimeWrappedException by the runtime, and as
				// such will be catched by the catch clause above
				catch
				{
					// Catch handler for non System.Exception types
					ErrorHandler.Error("Failed in Bulk DoAppend (unknown exception)");
				}
#endif
				finally
				{
					m_recursiveGuard = false;
				}
			}
		}

        private void EnsureQueueStarted()
        {
            if (!m_isQueueStarted)
            {
                m_isQueueStarted = true;
                if (Queue != null)
                {
                    Queue.Start();
                }
            }
        }

		#endregion Implementation of IBulkAppender


		#region Public Instance Methods


		#endregion Public Instance Methods

		#region Protected Instance Methods

        /// <summary>
        /// In a derived class, formats a logging event into a formatted object that can be queued.
        /// </summary>
        /// <param name="loggingEvent">The <see cref="LoggingEvent"/> to format.</param>
        /// <returns>The formatted logging event, or <c>null</c> to discard the logging event.</returns>
        /// <remarks>
        /// If this method is not overridden, <paramref name="loggingEvent"/> is returned unmodified.
        /// <para>
        /// The object returned by this method must be suitable for placing in the queue.  For example, to
        /// use a persistent queue, the object needs to be serializable.
        /// </para>
        /// </remarks>
        virtual protected object FormatLoggingEvent(LoggingEvent loggingEvent)
        {
            // By default there is no formatting.
            return loggingEvent;
        }

        private IList<object> FormatLoggingEvents(ICollection<LoggingEvent> loggingEvents)
        {
            List<object> formattedLoggingEvents = new List<object>(loggingEvents.Count);
            foreach(LoggingEvent loggingEvent in loggingEvents)
            {
                object formattedLoggingEvent = FormatLoggingEvent(loggingEvent);
                if (formattedLoggingEvent != null) formattedLoggingEvents.Add(formattedLoggingEvent);
            }
            return formattedLoggingEvents;
        }

        private void FixLoggingEvents(IEnumerable<LoggingEvent> loggingEvents)
        {
            foreach(LoggingEvent loggingEvent in loggingEvents)
            {
                loggingEvent.Fix = this.Fix;
            }
        }

        /// <summary>
        /// If all the objects in <paramref name="formattedLoggingEvents"/> are <see cref="LoggingEvent"/>
        /// instances, returns them in a list.  Otherwise, returns <c>null</c>.
        /// </summary>
        /// <param name="formattedLoggingEvents">A collection of formatted logging events.</param>
        /// <returns>A list of <see cref="LoggingEvent"/> objects if all objects are <see cref="LoggingEvent"/> instances; otherwise <c>null</c>.</returns>
        private IList<LoggingEvent> GetAsLoggingEvents(ICollection<object> formattedLoggingEvents)
        {
            List<LoggingEvent> loggingEvents = new List<LoggingEvent>(formattedLoggingEvents.Count);
            foreach (object formattedLoggingEvent in loggingEvents)
            {
                LoggingEvent loggingEvent = formattedLoggingEvent as LoggingEvent;
                if (loggingEvent == null) return null;
                loggingEvents.Add(loggingEvent);
            }
            return loggingEvents;

        }

        /// <summary>
        /// In a derived class that doesn't implement <see cref="FormatLoggingEvent"/>, appends
        /// <paramref name="loggingEvent"/> to the logging sink.
        /// </summary>
        /// <param name="loggingEvent">The <see cref="LoggingEvent"/> to append.</param>
        /// <remarks>
        /// We override <see cref="AppenderBase.Append(LoggingEvent)"/> so that derived classes that implement
        /// <see cref="FormatLoggingEvent"/> are not obliged to provide an unused implementation.
        /// </remarks>
        protected override void Append(LoggingEvent loggingEvent)
        {
        }

        /// <summary>
        /// In a derived class, appends formatted logging events to the logging sink.
        /// </summary>
        /// <param name="formattedLoggingEvents">The formatted logging events to be appended.
        /// </param>
        /// <remarks>
        /// This method is called instead of <see cref="AppenderBase.Append(LoggingEvent[])"/> in a derived class that overrides
        /// <see cref="FormatLoggingEvent"/> and returns an object of a type other than <see cref="LoggingEvent"/>.
        /// </remarks>
        virtual protected void AppendFormattedEvents(IList<object> formattedLoggingEvents)
        { 
        }

		#endregion

		/// <summary>
        /// In a derived class, flushes any buffered log data.
        /// </summary>
		/// <remarks>
		/// This implementation doesn't flush anything and always returns true
		/// </remarks>
        /// <returns><c>True</c> if all logging events were flushed successfully, else <c>false</c>.</returns>
        protected virtual bool Flush(int millisecondsTimeout)
        {
            return true;
        }

        bool IFlushable.Flush(int millisecondsTimeout)
        {
            if (!IsEnabled) return true;
            if (IsAsynchronous)
            {
                return Queue.Flush(millisecondsTimeout);
            }
            else
            {
                return this.Flush(millisecondsTimeout);
            }
        }

		#region Private Instance Fields

		/// <summary>
		/// Flag indicating if this appender is closed.
		/// </summary>
		/// <remarks>
		/// See <see cref="Close"/> for more information.
		/// </remarks>
		private bool m_closed = false;

		/// <summary>
		/// The guard prevents an appender from repeatedly calling its own DoAppend method
		/// </summary>
		private bool m_recursiveGuard = false;

        /// <summary>
        /// Flag indicating if this appender has been successfully configured by calling <see cref="IOptionHandler.ActivateOptions"/>
        /// </summary>
        public bool IsConfigured { get; private set; }

        /// <summary>
        /// Flag indicating whether the queue has been started when in asynchronous mode.
        /// </summary>
        private bool m_isQueueStarted;
        
		#endregion Private Instance Fields


	    #region Private Static Fields

	    /// <summary>
	    /// The fully qualified type of the AppenderSkeleton class.
	    /// </summary>
	    /// <remarks>
	    /// Used by the internal logger to record the Type of the
	    /// log message.
	    /// </remarks>
	    private readonly static Type declaringType = typeof(AppenderSkeleton);

	    #endregion Private Static Fields

        #region IDisposable

        /// <summary>
        /// Releases all resources used by the appender.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            // We don't implement a finalizer because it is highly unlikely any derived class would have
            // unmanaged resources.  But we call SuppressFinalize in case a derived class decides to do so.
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the unmanaged resources used by the appender and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!m_closed) Close();
        }

        #endregion



    }
}
