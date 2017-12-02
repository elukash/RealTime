using System;

namespace RealTime
{
    internal class WorkItem
    {
        public WorkItem(TimeSpan delay, Action task, bool inclusive)
        {
            Delay = delay;
            Task = task;
            Inclusive = inclusive;
        }

        public TimeSpan Delay { get; }

        public Action Task { get; }

        public bool Inclusive { get; }
    }
}
