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

using log4net.Filter;
using log4net.Util;
using log4net.Layout;
using log4net.Core;

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
	/// Appenders can also implement the <see cref="IOptionHandler"/> interface. Therefore
	/// they would require that the <see cref="M:IOptionHandler.ActivateOptions()"/> method
	/// be called after the appenders properties have been configured.
	/// </para>
	/// </remarks>
	/// <author>Nicko Cadell</author>
	/// <author>Gert Driesen</author>
	public abstract class AppenderSkeleton : AppenderBase, IAppender, IBulkAppender, IOptionHandler, IFlushable
	{
		#region Protected Instance Constructors

		/// <summary>
		/// Default constructor
		/// </summary>
		/// <remarks>
		/// <para>Empty default constructor</para>
		/// </remarks>
		protected AppenderSkeleton()
		{
		}

		#endregion Protected Instance Constructors

		#region Finalizer

		/// <summary>
		/// Finalizes this appender by calling the implementation's 
		/// <see cref="Close"/> method.
		/// </summary>
		/// <remarks>
		/// <para>
		/// If this appender has not been closed then the <c>Finalize</c> method
		/// will call <see cref="Close"/>.
		/// </para>
		/// </remarks>
		~AppenderSkeleton() 
		{
			// An appender might be closed then garbage collected. 
			// There is no point in closing twice.
			if (!m_closed) 
			{
				LogLog.Debug(declaringType, "Finalizing appender named ["+Name+"].");
				Close();
			}
		}

		#endregion Finalizer

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
		virtual public void ActivateOptions()
		{
		}

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
		/// delegates the closing of the appender to the <see cref="OnClose"/>
		/// method which must be overridden in the subclass.
		/// </para>
		/// </remarks>
		public void Close()
		{
			// This lock prevents the appender being closed while it is still appending
			lock(this)
			{
				if (!m_closed)
				{
					OnClose();
					m_closed = true;
				}
			}
		}

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
			// This lock is absolutely critical for correct formatting
			// of the message in a multi-threaded environment.  Without
			// this, the message may be broken up into elements from
			// multiple thread contexts (like get the wrong thread ID).

			lock(this)
			{
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
						this.Append(loggingEvent);
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
			// This lock is absolutely critical for correct formatting
			// of the message in a multi-threaded environment.  Without
			// this, the message may be broken up into elements from
			// multiple thread contexts (like get the wrong thread ID).

			lock(this)
			{
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

					ArrayList filteredEvents = new ArrayList(loggingEvents.Length);

					foreach(LoggingEvent loggingEvent in loggingEvents)
					{
						if (FilterEvent(loggingEvent))
						{
							filteredEvents.Add(loggingEvent);
						}
					}

					if (filteredEvents.Count > 0 && PreAppendCheck())
					{
						this.Append((LoggingEvent[])filteredEvents.ToArray(typeof(LoggingEvent)));
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

		#endregion Implementation of IBulkAppender


		#region Public Instance Methods

		#endregion Public Instance Methods

		#region Protected Instance Methods

		#endregion


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

		#endregion Private Instance Fields

		#region Constants


		#endregion

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
	}
}
