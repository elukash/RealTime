using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Reactive.Testing;
using Moq;
using NUnit.Framework;

namespace RealTime.Tests
{
    [TestFixture]
    public class ActionSchedulerTests
    {
        [Test]
        [TestCaseSource(nameof(SchedulerTestCases))]
        public async Task Can_Schedule_Task(TimeSpan startTime, bool inclusive, TimeSpan sampling, TimeSpan delay, TimeSpan workDuration, int occurs)
        {
            var invoker = GetFakeInvoker();
            var testScheduler = new TestScheduler();

            using (var scheduler = new ActionScheduler(sampling, testScheduler))
            {
                scheduler.Schedule(delay, () => invoker.Object.Invoke(), inclusive);

                testScheduler.AdvanceBy(startTime.Ticks);
                await scheduler.StartAsync();

                testScheduler.AdvanceBy(workDuration.Ticks - startTime.Ticks);
            }

            invoker.Verify(x => x.Invoke(), Times.Exactly(occurs));
        }

        [Test]
        public async Task Can_Schedule_Multiple_Tasks()
        {
            var task1 = GetFakeInvoker();
            var task2 = GetFakeInvoker();
            var testScheduler = new TestScheduler();

            using (var scheduler = new ActionScheduler(TimeSpan.FromSeconds(5), testScheduler))
            {
                scheduler.Schedule(TimeSpan.FromSeconds(1), () => task1.Object.Invoke());
                scheduler.Schedule(TimeSpan.FromSeconds(2), () => task2.Object.Invoke());

                await scheduler.StartAsync();

                testScheduler.AdvanceBy(TimeSpan.FromSeconds(10).Ticks);
            }

            task1.Verify(x => x.Invoke(), Times.Exactly(2));
            task2.Verify(x => x.Invoke(), Times.Exactly(2));
        }

        [Test]
        public async Task Scheduled_Tasks_Runs_Exactly_In_Time()
        {
            var task1 = GetFakeTimeInvoker();
            var task2 = GetFakeTimeInvoker();

            var sampleInSeconds = 5;
            var firstDelay = 2;
            var secondDelay = 3;

            var sampling = TimeSpan.FromSeconds(sampleInSeconds);
            DateTimeOffset startTime;
            var testScheduler = new TestScheduler();

            using (var scheduler = new ActionScheduler(sampling, testScheduler))
            {
                scheduler.Schedule(TimeSpan.FromSeconds(firstDelay), () => task1.Object.Invoke(testScheduler.Now));
                scheduler.Schedule(TimeSpan.FromSeconds(secondDelay), () => task2.Object.Invoke(testScheduler.Now));

                startTime = testScheduler.Now;
                await scheduler.StartAsync();

                testScheduler.AdvanceBy(TimeSpan.FromSeconds(10).Ticks);
            }

            var correction = startTime.AddSeconds(-(startTime.Second % sampleInSeconds));

            var firstTime = correction.AddSeconds(firstDelay);
            if (startTime > firstTime)
            {
                firstTime = firstTime.Add(sampling);
            }

            var secondTime = correction.AddSeconds(secondDelay);
            if (startTime > secondTime)
            {
                secondTime = secondTime.Add(sampling);
            }

            task1.Verify(x => x.Invoke(It.Is<DateTimeOffset>(date => DateEqual(date, firstTime))), Times.Exactly(1));
            task1.Verify(x => x.Invoke(It.Is<DateTimeOffset>(date => DateEqual(date, firstTime.Add(sampling)))), Times.Exactly(1));

            task2.Verify(x => x.Invoke(It.Is<DateTimeOffset>(date => DateEqual(date, secondTime))), Times.Exactly(1));
            task2.Verify(
                x => x.Invoke(It.Is<DateTimeOffset>(date => DateEqual(date, secondTime.Add(sampling)))),
                Times.Exactly(1));
        }

        private static IEnumerable<TestCaseData> SchedulerTestCases()
        {
            yield return
                new TestCaseData(TimeSpan.Zero, false, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10), 2)
                    .SetName("Several planned occurs");
            yield return
                new TestCaseData(TimeSpan.Zero, false, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(20), 4)
                    .SetName("More occurs");
            yield return
                new TestCaseData(TimeSpan.Zero, false, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(5), 0)
                    .SetName("Zero occurs");
            yield return
                new TestCaseData(TimeSpan.FromSeconds(4), false, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(20), 1)
                    .SetName("Non-inclusive task, one occurs");
            yield return
                new TestCaseData(TimeSpan.FromSeconds(4), true, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(20), 2)
                    .SetName("Inclusive task, two occurs");
        }

        private bool DateEqual(DateTimeOffset left, DateTimeOffset right)
        {
            return left.Hour == right.Hour && left.Minute == right.Minute && left.Second == right.Second;
        }

        private Mock<IInvoker> GetFakeInvoker()
        {
            var task = new Mock<IInvoker>();
            task.Setup(x => x.Invoke());

            return task;
        }

        private Mock<ITimeInvoker> GetFakeTimeInvoker()
        {
            var task = new Mock<ITimeInvoker>();
            task.Setup(x => x.Invoke(It.IsAny<DateTimeOffset>()));

            return task;
        }

        public interface IInvoker
        {
            void Invoke();
        }

        public interface ITimeInvoker
        {
            void Invoke(DateTimeOffset time);
        }
    }
}
