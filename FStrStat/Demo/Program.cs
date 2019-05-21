using FStrStat.Core;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MathNet.Numerics.Random;
using MathNet.Numerics.Distributions;

namespace Demo
{
    public abstract class FailsafeProcess
    {
        private long failureCount;
        private double successRate;

        public bool Test(double sample)
        {
            successRate = AdjuestSuccessRate(failureCount);
            if (sample < successRate)
            {
                failureCount = 0;
                return true;
            }
            else
            {
                failureCount++;
                return false;
            }
        }

        protected abstract double AdjuestSuccessRate(long failureCount);
    }

    class Example:
        FailsafeProcess
    {
        private const double DefaultRate = 0.02;
        private const double Step = 0.02;
        private const long FailureThreshold = 50;

        protected override double AdjuestSuccessRate(long failureCount)
        {
            //if (failureCount < FailureThreshold)
            //{
            //    return DefaultRate;
            //}
            //else
            //{
            //    var adjustment = Step * (failureCount - FailureThreshold);
            //    return Math.Min(DefaultRate + adjustment, 1.0);
            //}
            return DefaultRate;
        }
    }
    class Program
    {
        const int ThreadCount = 7;
        const long TestCount = 14_000_000_000;
        static async Task Main(string[] args)
        {
            var tasks = new List<Task>();
            var param = new SimulatorParams()
            {
                RandomSourceFactory = () => new ContinuousUniform(0, 1, new MersenneTwister()),
                RenewRandomSourceAfter = 2L << 30,
                TestFactory = () => new Example().Test,
            };
            var sim = new Simulator(param);
            var source = new CancellationTokenSource();
            for(int i = 0; i < ThreadCount; ++i)
            {
                tasks.Add(sim.RunSimulation(source.Token));
                await Task.Delay(50);
            }
            for(; ; )
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                double mean, min, max, stddev;
                long count;
                lock(sim.Accumulator)
                {
                    mean = sim.Accumulator.Mean;
                    min = sim.Accumulator.Minimum;
                    max = sim.Accumulator.Maximum;
                    stddev = sim.Accumulator.StandardDeviation;
                    count = sim.Accumulator.Count;
                }

                Console.WriteLine(
                    $"Count:{count:##,#}, Mean: {mean}±{stddev}, Min:{min}, Max:{max}");

                if(count >= TestCount)
                {
                    source.Cancel();
                    break;
                }
            }
            await Task.WhenAny(Task.WhenAll(tasks), Task.Delay(Timeout.InfiniteTimeSpan, source.Token));
        }
    }
}
