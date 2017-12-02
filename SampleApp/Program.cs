using System;
using System.Reactive.Concurrency;
using RealTime;

namespace SampleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            var tenSeconds = TimeSpan.FromSeconds(10).Negate();
            var scheduler = new ActionScheduler(tenSeconds);

            scheduler.Schedule(
                TimeSpan.FromSeconds(1),
                () => WriteLine("First second"));

            scheduler.Schedule(
                TimeSpan.FromSeconds(5),
                () => WriteLine("Fifth second"));

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
