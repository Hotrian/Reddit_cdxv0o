using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

#if DEBUGGING
using System.Diagnostics;
#endif

namespace Honey.Threading
{
    public delegate void MainThreadExecuteMessage();
    public delegate void ThreadExecuteMessage(ThreadContainer container);

    public static class ThreadManager
    {
        public static Thread MainThread
        {
            get;
            private set;
        }

        public static bool StopRequested { get; private set; }
        public static ThreadContainer[] Threads;

        public static bool HasInit;

        // Holds actions to be dispatched on non-main threads
        public static readonly object ExecuteLocker = new object();
        private static readonly List<ThreadExecuteMessage> ExecuteQueue = new List<ThreadExecuteMessage>();

        // Holds actions to be dispatched on the main thread
        public static readonly object MainThreadExecuteLocker = new object();
        private static readonly List<MainThreadExecuteMessage> ExecuteOnMainThreadQueue = new List<MainThreadExecuteMessage>();

#if DEBUGGING
        private static readonly object TimerLocker = new object();
        private static readonly Dictionary<string, TimerAverageData> TimerDictionary = new Dictionary<string, TimerAverageData>();
#endif

        #region Thread Manager Core
        public static void Init(int threads = 16)
        {
            if (HasInit) return;
            HasInit = true;

            // Initialize our thread pool
            Threads = new ThreadContainer[threads];
            for (var i = 0; i < threads; i++)
            {
                Threads[i] = new ThreadContainer(i, new Thread(ThreadCode));
                Threads[i].Thread.Start(Threads[i]);
            }

            MainThread = Thread.CurrentThread;
        }

        public static void Update()
        {
            if (StopRequested) return;
            // Process actions for the main thread
            var executeOnMainThread = GetAllExecuteOnMainThread();
            if (executeOnMainThread != null && executeOnMainThread.Length > 0)
            {
                foreach (var executeMessage in executeOnMainThread)
                {
                    executeMessage();
                }
            }
            // Process actions for the thread pool
            for (var i = 0; i < Threads.Length; i++)
            {
                if (Threads[i].GetBusy())
                {
                    continue;
                }
                var execute = GetNextExecute();
                if (execute == null) break;
                Threads[i].Execute(execute);
            }
        }

        public static bool IsMainThread()
        {
            return Thread.CurrentThread == MainThread;
        }

        // Attempt to close all threads
        public static void Shutdown()
        {
            StopRequested = true;
            for (var i = 0; i < Threads.Length; i++)
            {
                Threads[i].AutoResetEvent.Set();
            }
            for (var i = 0; i < Threads.Length; i++)
            {
                Threads[i].Thread.Abort();
            }
            for (var i = 0; i < Threads.Length; i++)
            {
                Threads[i].AutoResetEvent.Set();
            }
        }
        #endregion

        #region Execute on Threads
        // Execute an action on our thread pool
        public static void Execute(ThreadExecuteMessage msg)
        {
            if (StopRequested) return;
            lock (ExecuteLocker)
            {
#if DEBUGGING
                TotalJobs++;
#endif
                ExecuteQueue.Add(msg);
            }
        }
        // Execute an action on our thread pool, pushing this action to the top of the queue
        public static void ExecuteNext(ThreadExecuteMessage msg)
        {
            if (StopRequested) return;
            lock (ExecuteLocker)
            {
#if DEBUGGING
                TotalJobs++;
#endif
                ExecuteQueue.Insert(0, msg);
            }
        }
        // Get the next action to be executed
        private static ThreadExecuteMessage GetNextExecute()
        {
            if (StopRequested) return null;
            ThreadExecuteMessage ex;
            lock (ExecuteLocker)
            {
                if (ExecuteQueue.Count <= 0) return null;
#if DEBUGGING
                TotalJobs--;
#endif
                ex = ExecuteQueue[0];
                ExecuteQueue.RemoveAt(0);
            }
            return ex;
        }
        #endregion

        #region Execute on Main Thread
        // Execute an action on the main thread
        public static void ExecuteOnMainThreadAndWait(MainThreadExecuteMessage msg)
        {
            if (StopRequested) return;
            // Make a wait flag
            var r = new AutoResetEvent(false);
            // Inject the flag setter at the end of the action
            msg += () =>
            {
                r.Set();
            };
            // Push the action into the main thread queue for execution
            lock (MainThreadExecuteLocker)
            {
                ExecuteOnMainThreadQueue.Add(msg);
            }
            // Wait until the flag has been set
            r.WaitOne();
        }
        // Execute an action on the main thread
        public static void ExecuteOnMainThread(MainThreadExecuteMessage msg)
        {
            if (StopRequested) return;
            // Push the action into the main thread queue for execution
            lock (MainThreadExecuteLocker)
            {
                ExecuteOnMainThreadQueue.Add(msg);
            }
        }
        // Get the next action to be executed on the main thread
        private static MainThreadExecuteMessage GetNextExecuteOnMainThread()
        {
            if (StopRequested) return null;
            MainThreadExecuteMessage ex;
            lock (MainThreadExecuteLocker)
            {
                if (ExecuteOnMainThreadQueue.Count <= 0) return null;
                ex = ExecuteOnMainThreadQueue[0];
                ExecuteOnMainThreadQueue.RemoveAt(0);
            }
            return ex;
        }
        // Get the next action to be executed on the main thread
        private static MainThreadExecuteMessage[] GetAllExecuteOnMainThread()
        {
            if (StopRequested) return null;
            var ex = new List<MainThreadExecuteMessage>();
            lock (MainThreadExecuteLocker)
            {
                if (ExecuteOnMainThreadQueue.Count <= 0) return null;
                ex.AddRange(ExecuteOnMainThreadQueue);
                ExecuteOnMainThreadQueue.Clear();
            }
            return ex.ToArray();
        }
        #endregion

        #region Thread Loop Code
        // Loop for our threads to process actions
        private static void ThreadCode(object data)
        {
            var container = (ThreadContainer)data;
            var autoResetEvent = container.AutoResetEvent;
            while (!StopRequested)
            {
                try
                {
                    // Get an action if there is one queued
                    var ex = container.GetNextExecute();
                    if (ex == null)
                    {
                        // Sleep when no actions queued
                        if (StopRequested) break;
                        autoResetEvent.WaitOne();
                        continue;
                    }
                    // Process action
                    ex(container);
                    container.RemoveExecute(ex);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError(e);
                }
            }
        }
        #endregion

        #region Debugging
#if DEBUGGING
        public static int TotalJobs;
#endif
        public static int GetTotalJobs()
        {
#if DEBUGGING
            int j;
            lock (ExecuteLocker)
            {
                j = TotalJobs;
            }
            return j;
#else
            return 0;
#endif
        }

        public static void ExecuteTimed(string methodName, ThreadExecuteMessage msg)
        {
#if DEBUGGING
            var time = new Stopwatch();
            lock (TimerLocker)
            {
                if (!TimerDictionary.ContainsKey(methodName))
                    TimerDictionary.Add(methodName, new TimerAverageData(new long[0], TimerAverageData.DefaultSamples));
            }
            Execute((container =>
            {
                time.Start();
                container.WaitList.Clear();
                msg(container);
                foreach (var autoResetEvent in container.WaitList)
                {
                    autoResetEvent.WaitOne();
                }
                time.Stop();
                long[] s;
                int ms;
                lock (TimerLocker)
                {
                    s = TimerDictionary[methodName].ElapsedMilliseconds;
                    ms = TimerDictionary[methodName].MaxSamples;
                }
                if (s.Length > ms)
                {
                    var diff = s.Length - ms;
                    var s2 = new long[ms];
                    for (var i = 0; i < s2.Length - 1; i++)
                    {
                        s2[i] = s[diff + i];
                    }
                    s2[s2.Length - 1] = time.ElapsedMilliseconds;
                    s = s2;
                }
                else if (s.Length < ms)
                {
                    var s2 = new long[s.Length + 1];
                    for (var i = 0; i < s.Length; i++)
                    {
                        s2[i] = s[i];
                    }
                    s2[s2.Length - 1] = time.ElapsedMilliseconds;
                    s = s2;
                }
                else
                {
                    for (var i = 0; i < s.Length - 1; i++)
                    {
                        s[i] = s[i + 1];
                    }
                    s[s.Length - 1] = time.ElapsedMilliseconds;
                }
                lock (TimerLocker)
                {
                    TimerDictionary[methodName] = new TimerAverageData(s, ms);
                }
            }));
#else
            Execute(msg);
#endif
        }

        public static TimerData[] GetTimerData()
        {
#if DEBUGGING
            var data = new List<TimerData>();
            lock (TimerLocker)
            {
                data.AddRange(TimerDictionary.Select(kvp => new TimerData(kvp.Key, kvp.Value.GetAverage())));
            }
            return data.ToArray();
#else
            return null;
#endif
        }

        public struct TimerData
        {
            public readonly string Name;
            public readonly long ElapsedMilliseconds;

            public TimerData(string name, long elapsedMilliseconds)
            {
                Name = name;
                ElapsedMilliseconds = elapsedMilliseconds;
            }
        }

        public struct TimerAverageData
        {
            public const int DefaultSamples = 10;

            public readonly int MaxSamples;

            public readonly long[] ElapsedMilliseconds;

            public TimerAverageData(long[] elapsedMilliseconds, int maxSamples)
            {
                ElapsedMilliseconds = elapsedMilliseconds;
                MaxSamples = maxSamples;
            }

            public long GetAverage()
            {
                if (ElapsedMilliseconds.Length == 0) return 0;
                return ElapsedMilliseconds.Sum() / ElapsedMilliseconds.Length;
            }
        }
        #endregion
    }
}