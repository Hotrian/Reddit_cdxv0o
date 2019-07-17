using System.Collections.Generic;
using System.Threading;

namespace Honey.Threading
{
    /// <summary>
    /// ThreadContainer holds information for the Thread it contains
    /// </summary>
    public struct ThreadContainer
    {
        public readonly int Id;
        public readonly Thread Thread;
        public readonly AutoResetEvent AutoResetEvent;
        public readonly object ExecuteLocker;

        private readonly List<ThreadExecuteMessage> _executeQueue; // A queue is used in case we somehow end up with more than one action
#if DEBUGGING
        public readonly List<AutoResetEvent> WaitList;
#endif

        public ThreadContainer(int id, Thread thread) : this()
        {
            Id = id;
            Thread = thread;
            AutoResetEvent = new AutoResetEvent(false);
            ExecuteLocker = new object();
            _executeQueue = new List<ThreadExecuteMessage>();
#if DEBUGGING
            WaitList = new List<AutoResetEvent>();
#endif
        }
        // Returns true when we have an action queued
        public bool GetBusy()
        {
            if (ThreadManager.StopRequested) return true;
            lock (ExecuteLocker)
            {
                return (_executeQueue.Count > 0);
            }
        }
        // Enqueue an action on this thread
        public void Execute(ThreadExecuteMessage msg)
        {
            if (ThreadManager.StopRequested) return;
            lock (ExecuteLocker)
            {
                _executeQueue.Add(msg);
                AutoResetEvent.Set();
            }
        }
        // Gets the next action queued on this thread
        public ThreadExecuteMessage GetNextExecute()
        {
            if (ThreadManager.StopRequested) return null;
            ThreadExecuteMessage ex;
            lock (ExecuteLocker)
            {
                if (_executeQueue.Count <= 0)
                {
                    return null;
                }
                ex = _executeQueue[0];
            }
            return ex;
        }
        // Removes an action from the thread queue
        public void RemoveExecute(ThreadExecuteMessage msg)
        {
            if (ThreadManager.StopRequested) return;
            lock (ExecuteLocker)
            {
                _executeQueue.Remove(msg);
            }
        }
    }
}