using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace RealTime
{
    /// <summary>
    /// Represents task scheduler, that runs tasks periodically within specified sample timespan
    /// </summary>
    /// <seealso cref="System.IDisposable" />
    public class ActionScheduler : IDisposable
    {
        private readonly IScheduler _systemScheduler;

        private readonly TimeSpan _sampling;

        private readonly ConcurrentQueue<WorkItem> _tasks = new ConcurrentQueue<WorkItem>();

        private IEnumerable<IDisposable> _workingTasks;

        public ActionScheduler(TimeSpan sampling, IScheduler systemScheduler = null)
        {
            _systemScheduler = systemScheduler ?? Scheduler.Default;
            _sampling = sampling;
        }
        
        /// <summary>
        /// Schedules the task with specified delay.
        /// </summary>
        /// <param name="delay">The delay within sample.</param>
        /// <param name="task">The task to execute.</param>
        /// <param name="inclusive">Indicates whether task must be executed if delay was passed earlier</param>
        /// <exception cref="System.ArgumentException">
        /// Task excecution time must be within sampling
        /// </exception>
        public void Schedule(TimeSpan delay, Action task, bool inclusive = false)
        {
            if (delay > _sampling)
            {
                throw new ArgumentException("Task excecution time must be within sampling");
            }

            if (task == null)
            {
                throw new ArgumentException(nameof(task));
            }

            _tasks.Enqueue(new WorkItem(delay, task, inclusive));
        }

        /// <summary>
        /// Starts running tasks.
        /// </summary>
        public async Task StartAsync()
        {
            _workingTasks = await Task.WhenAll(_tasks.Select(
                async workItem =>
                {
                    var taskNextTime = GetSampleBeginTime().Add(workItem.Delay);
                    if (taskNextTime >= _systemScheduler.Now)
                    {
                        // task time in the future, just schedule and return 
                        return _systemScheduler.Schedule(
                            0,
                            taskNextTime,
                            (scheduler, state) => RunAndSchedule(workItem.Task, _sampling));
                    }

                    // task time elapsed in the sample. we should run immediately if task is inclusive and perform time correction 
                    if (workItem.Inclusive)
                    {
                        await Task.Run(() => workItem.Task());
                    }

                    taskNextTime = taskNextTime.Add(_sampling);

                    return _systemScheduler.Schedule(
                        0,
                        taskNextTime,
                        (scheduler, state) => RunAndSchedule(workItem.Task, _sampling));
                }).ToArray());
        }

        public void Stop()
        {
            if (_workingTasks == null)
            {
                return;
            }

            foreach (var workingTask in _workingTasks)
            {
                workingTask.Dispose();
            }
        }

        public void Dispose()
        {
            Stop();
        }
        
        public DateTimeOffset GetSampleBeginTime()
        {
            var now = _systemScheduler.Now;

            return new DateTimeOffset(now.Ticks - (now.Ticks % _sampling.Ticks), DateTimeOffset.UtcNow.Offset);
        }

        /// <summary>
        /// Generates periodic infinitive sequence with period normilization. Time drift is normilized in every iteration
        /// </summary>
        /// <param name="period">The period.</param>
        /// <returns></returns>
        private IObservable<long> PeriodicTimer(TimeSpan period)
        {
            return Observable.Create<long>(observer =>
            {
                var next = _systemScheduler.Now + period;
                return _systemScheduler.Schedule(
                    0,
                    period,
                    (t, rec) =>
                    {
                        observer.OnNext(t);

                        next += period;
                        rec(0, Scheduler.Normalize(next - _systemScheduler.Now));
                    });
            });
        }

        private IDisposable RunAndSchedule(Action task, TimeSpan period)
        {
            task();
            return PeriodicTimer(period).Subscribe(x => task());
        }
    }
}
