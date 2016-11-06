using log4net.Core;
using log4net.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace log4net.Appender
{
    /// <summary>
    /// This class is identical to <see cref="ForwardingAppender"/>, except that it derives from
    /// <see cref="AsyncAppenderSkeleton"/> rather than <see cref="AppenderSkeleton"/>, and can thus
    /// forward events asynchronously.
    /// </summary>
    public class AsyncForwardingAppender : AsyncAppenderSkeleton, IAppenderAttachable
    {
        // TODO: review locking

		#region Public Instance Constructors

		/// <summary>
		/// Initializes a new instance of the <see cref="AsyncForwardingAppender" /> class.
		/// </summary>
		/// <remarks>
		/// <para>
		/// Default constructor.
		/// </para>
		/// </remarks>
		public AsyncForwardingAppender()
		{
		}

		#endregion Public Instance Constructors

        #region Override implementation of AsyncAppenderSkeleton

        /// <summary>
        /// Closes the appender and releases resources.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Releases any resources allocated within the appender such as file handles, 
        /// network connections, etc.
        /// </para>
        /// <para>
        /// It is a programming error to append to a closed appender.
        /// </para>
        /// </remarks>
        override protected void OnClose()
        {
            // Remove all the attached appenders
            lock (this)
            {
                if (m_appenderAttachedImpl != null)
                {
                    m_appenderAttachedImpl.RemoveAllAppenders();
                }
            }
        }

        /// <summary>
        /// Forward the logging event to the attached appenders 
        /// </summary>
        /// <param name="loggingEvent">The event to log.</param>
        /// <remarks>
        /// <para>
        /// Delivers the logging event to all the attached appenders.
        /// </para>
        /// </remarks>
        override protected void Append(LoggingEvent loggingEvent)
        {
            // Pass the logging event on the the attached appenders
            if (m_appenderAttachedImpl != null)
            {
                m_appenderAttachedImpl.AppendLoopOnAppenders(loggingEvent);
            }
        }

        /// <summary>
        /// Forward the logging events to the attached appenders 
        /// </summary>
        /// <param name="loggingEvents">The array of events to log.</param>
        /// <remarks>
        /// <para>
        /// Delivers the logging events to all the attached appenders.
        /// </para>
        /// </remarks>
        override protected void Append(LoggingEvent[] loggingEvents)
        {
            // Pass the logging event on the the attached appenders
            if (m_appenderAttachedImpl != null)
            {
                m_appenderAttachedImpl.AppendLoopOnAppenders(loggingEvents);
            }
        }

        private static int GetWaitTime(DateTime startTimeUtc, int millisecondsTimeout)
        {
            if (millisecondsTimeout == Timeout.Infinite) return Timeout.Infinite;
            if (millisecondsTimeout == 0) return 0;

            int elapsedMilliseconds = (int)(DateTime.UtcNow - startTimeUtc).TotalMilliseconds;
            int timeout = millisecondsTimeout - elapsedMilliseconds;
            if (timeout < 0) timeout = 0;
            return timeout;
        }

        protected override bool Flush(int millisecondsTimeout)
        {
            // TODO: throw or just ignore invalid timeout?
            //if (millisecondsTimeout < -1) throw new ArgumentOutOfRangeException("millisecondsTimeout", "Timeout must be -1 (Timeout.Infinite) or non-negative");
            if (millisecondsTimeout < -1) return false;

            // Assume success until one of the appenders fails
            bool result = true;

            // Use DateTime.UtcNow rather than a System.Diagnostics.Stopwatch for compatibility with .NET 1.x
            DateTime startTimeUtc = DateTime.UtcNow;

            // First tell all attached appenders to start (trigger) flushing, without waiting
            if (millisecondsTimeout != 0) Flush(0);

            // TODO: locking
            if (m_appenderAttachedImpl != null)
            {
                foreach(IAppender appender in m_appenderAttachedImpl.Appenders)
                {
                    log4net.Appender.IFlushable flushable = appender as log4net.Appender.IFlushable;
                    if (flushable == null) continue;
                    int timeout = GetWaitTime(startTimeUtc, millisecondsTimeout);
                    if (!flushable.Flush(timeout)) result = false;
                }
            }

            return result;

        }

        #endregion Override implementation of AppenderSkeleton


        #region Implementation of IAppenderAttachable

        /// <summary>
        /// Adds an <see cref="IAppender" /> to the list of appenders of this
        /// instance.
        /// </summary>
        /// <param name="newAppender">The <see cref="IAppender" /> to add to this appender.</param>
        /// <remarks>
        /// <para>
        /// If the specified <see cref="IAppender" /> is already in the list of
        /// appenders, then it won't be added again.
        /// </para>
        /// </remarks>
        virtual public void AddAppender(IAppender newAppender)
        {
            if (newAppender == null)
            {
                throw new ArgumentNullException("newAppender");
            }
            lock (this)
            {
                if (m_appenderAttachedImpl == null)
                {
                    m_appenderAttachedImpl = new log4net.Util.AppenderAttachedImpl();
                }
                m_appenderAttachedImpl.AddAppender(newAppender);
            }
        }

        /// <summary>
        /// Gets the appenders contained in this appender as an 
        /// <see cref="System.Collections.ICollection"/>.
        /// </summary>
        /// <remarks>
        /// If no appenders can be found, then an <see cref="EmptyCollection"/> 
        /// is returned.
        /// </remarks>
        /// <returns>
        /// A collection of the appenders in this appender.
        /// </returns>
        virtual public AppenderCollection Appenders
        {
            get
            {
                lock (this)
                {
                    if (m_appenderAttachedImpl == null)
                    {
                        return AppenderCollection.EmptyCollection;
                    }
                    else
                    {
                        return m_appenderAttachedImpl.Appenders;
                    }
                }
            }
        }

        /// <summary>
        /// Looks for the appender with the specified name.
        /// </summary>
        /// <param name="name">The name of the appender to lookup.</param>
        /// <returns>
        /// The appender with the specified name, or <c>null</c>.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Get the named appender attached to this buffering appender.
        /// </para>
        /// </remarks>
        virtual public IAppender GetAppender(string name)
        {
            lock (this)
            {
                if (m_appenderAttachedImpl == null || name == null)
                {
                    return null;
                }

                return m_appenderAttachedImpl.GetAppender(name);
            }
        }

        /// <summary>
        /// Removes all previously added appenders from this appender.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is useful when re-reading configuration information.
        /// </para>
        /// </remarks>
        virtual public void RemoveAllAppenders()
        {
            lock (this)
            {
                if (m_appenderAttachedImpl != null)
                {
                    m_appenderAttachedImpl.RemoveAllAppenders();
                    m_appenderAttachedImpl = null;
                }
            }
        }

        /// <summary>
        /// Removes the specified appender from the list of appenders.
        /// </summary>
        /// <param name="appender">The appender to remove.</param>
        /// <returns>The appender removed from the list</returns>
        /// <remarks>
        /// The appender removed is not closed.
        /// If you are discarding the appender you must call
        /// <see cref="IAppender.Close"/> on the appender removed.
        /// </remarks>
        virtual public IAppender RemoveAppender(IAppender appender)
        {
            lock (this)
            {
                if (appender != null && m_appenderAttachedImpl != null)
                {
                    return m_appenderAttachedImpl.RemoveAppender(appender);
                }
            }
            return null;
        }

        /// <summary>
        /// Removes the appender with the specified name from the list of appenders.
        /// </summary>
        /// <param name="name">The name of the appender to remove.</param>
        /// <returns>The appender removed from the list</returns>
        /// <remarks>
        /// The appender removed is not closed.
        /// If you are discarding the appender you must call
        /// <see cref="IAppender.Close"/> on the appender removed.
        /// </remarks>
        virtual public IAppender RemoveAppender(string name)
        {
            lock (this)
            {
                if (name != null && m_appenderAttachedImpl != null)
                {
                    return m_appenderAttachedImpl.RemoveAppender(name);
                }
            }
            return null;
        }

        #endregion Implementation of IAppenderAttachable


        #region Private Instance Fields

        /// <summary>
        /// Implementation of the <see cref="IAppenderAttachable"/> interface
        /// </summary>
        private AppenderAttachedImpl m_appenderAttachedImpl;

        #endregion Private Instance Fields

    }
}
