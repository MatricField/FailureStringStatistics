using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MathNet.Numerics.Distributions;
using MathNet.Numerics.Statistics;

namespace FStrStat.Core
{
    public class SimulatorParams
    {
        public Func<IContinuousDistribution> RandomSourceFactory { get; set; }

        public Func<Func<double, bool>> TestFactory { get; set; }

        public long RenewRandomSourceAfter { get; set; }

    }
    public class Simulator
    {
        private Func<IContinuousDistribution> randomSourceFactory;
        private Func<Func<double, bool>> testFactory;
        private long renewRandomSourceAfter;

        public RunningStatistics Accumulator { get; } = new RunningStatistics();

        public Simulator(SimulatorParams param)
        {
            randomSourceFactory = param.RandomSourceFactory ?? throw new ArgumentNullException();
            testFactory = param.TestFactory ?? throw new ArgumentNullException();
            renewRandomSourceAfter = param.RenewRandomSourceAfter > 0 ? param.RenewRandomSourceAfter : throw new ArgumentException();
        }

        public Task RunSimulation(CancellationToken token)
        {
            var test = testFactory();
            var randSource = randomSourceFactory();
            var sampleCount = 0;
            void CheckRandSourceRenew()
            {
                if(sampleCount > renewRandomSourceAfter)
                {
                    randSource = randomSourceFactory();
                }
            }
            double GetNextFailureStringLength()
            {
                double failureStringLength = 0;
                for(; ; )
                {
                    token.ThrowIfCancellationRequested();
                    if(test(randSource.Sample()))
                    {
                        break;
                    }
                    else
                    {
                        ++failureStringLength;
                        ++sampleCount;
                        CheckRandSourceRenew();
                    }
                    
                }
                return failureStringLength;
            }

            void MainLoop()
            {
                for (; ; )
                {
                    token.ThrowIfCancellationRequested();
                    ReportFailureStringLength(GetNextFailureStringLength());
                }
            }

            return Task.Run(MainLoop, token);
        }

        private void ReportFailureStringLength(double length)
        {
            lock(Accumulator)
            {
                Accumulator.Push(length);
            }
        }
    }
}
