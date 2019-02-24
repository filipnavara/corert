// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.Runtime.Augments;
using Microsoft.Win32.SafeHandles;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace System.Threading
{
    //
    // Unix-specific implementation of Timer
    //
    internal partial class TimerQueue : IThreadPoolWorkItem
    {
        private static List<int> s_timerIDsToFire;

        /// <summary>
        /// This event is used by the timer thread to wait for timer expiration. It is also
        /// used to notify the timer thread that a new timer has been set.
        /// </summary>
        private static readonly AutoResetEvent s_timerEvent = new AutoResetEvent(false);

        private int _nextTimerDue = -1;

        private static void EnsureTimerThreadInitialized()
        {
            if (s_timerIDsToFire == null)
            {
                RuntimeThread timerThread = RuntimeThread.Create(TimerThread);
                timerThread.IsBackground = true;
                timerThread.Start();

                s_timerIDsToFire = new List<int>(Instances.Count);
            }
        }

        private bool SetTimer(uint actualDuration)
        {
            Debug.Assert((int)actualDuration >= 0);
            AutoResetEvent timerEvent = s_timerEvent;

            lock (timerEvent)
            {
                _nextTimerDue = TickCount + (int)actualDuration;
                EnsureTimerThreadInitialized();
            }

            timerEvent.Set();
            return true;
        }

        /// <summary>
        /// This method is executed on a dedicated a timer thread. Its purpose is
        /// to handle timer requests and notify the TimerQueue when a timer expires.
        /// </summary>
        private static void TimerThread()
        {
            AutoResetEvent timerEvent = s_timerEvent;
            List<int> timerIDsToFire;
            lock (timerEvent)
            {
                timerIDsToFire = s_timerIDsToFire;
            }

            int shortestWaitDurationMs = Timeout.Infinite;
            while (true)
            {
                timerEvent.WaitOne(shortestWaitDurationMs);

                int currentTimeMs = TickCount;
                shortestWaitDurationMs = int.MaxValue;
                lock (timerEvent)
                {
                    for (int i = Instances.Count - 1; i >= 0; --i)
                    {
                        if (Instances[i]._nextTimerDue != -1)
                        {
                            int waitDurationMs = Instances[i]._nextTimerDue - currentTimeMs;
                            if (waitDurationMs <= 0)
                            {
                                Instances[i]._nextTimerDue = -1;
                                timerIDsToFire.Add(timers[i].id);
                            }
                            else if (waitDurationMs < shortestWaitDurationMs)
                            {
                                shortestWaitDurationMs = waitDurationMs;
                            }
                        }
                    }
                }

                if (timerIDsToFire.Count > 0)
                {
                    foreach (int timerIDToFire in timerIDsToFire)
                    {
                        ThreadPool.UnsafeQueueUserWorkItemInternal(Instances[timerIDToFire], preferLocal: false);
                    }
                    timerIDsToFire.Clear();
                }

                if (shortestWaitDurationMs == int.MaxValue)
                {
                    shortestWaitDurationMs = Timeout.Infinite;
                }
            }
        }

        private static int TickCount => Environment.TickCount;
        void IThreadPoolWorkItem.Execute() => FireNextTimers();
    }
}
