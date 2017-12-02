using System;
using System.Reactive.Concurrency;
using System.Threading.Tasks;
using RealTime;

namespace SampleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            var tenSeconds = TimeSpan.FromSeconds(10);
            var scheduler = new ActionScheduler(tenSeconds);

            scheduler.Schedule(
                TimeSpan.FromSeconds(1),
                () => WriteLine("First second"));

            scheduler.Schedule(
                TimeSpan.FromSeconds(5),
                () => WriteLine("Fifth second"));

            // this task run with delay but will be executed in correct time in the next iteration
            scheduler.Schedule(
                TimeSpan.FromSeconds(5),
                async () =>
                {
                    WriteLine("Fifth second with delay");
                    await Task.Delay(TimeSpan.FromSeconds(1));
                });

            scheduler.Start();
            Console.ReadKey();
            scheduler.Stop();
        }

        private static void WriteLine(string message)
        {
            Console.WriteLine($"{message}. Time: {DateTime.Now}");
        }
    }
}
