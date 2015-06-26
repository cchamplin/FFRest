using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TranscodeProcessor
{
    public static class TaskExecutorFactory
    {
        public static Task Begin(Action action,
                                int interval = Timeout.Infinite,
                                int delay = 0,
                                int runTime = Timeout.Infinite,
                                int maxRuns = -1,
                                CancellationToken cancelToken = new CancellationToken(),
                                TaskCreationOptions taskOptions = TaskCreationOptions.None)
        {
            Stopwatch sw = new Stopwatch();
            Action wrapper = () =>
            {
                StopIfCancelled(cancelToken);
                action();
            };

            Action executor = () =>
            {
                ExecutorTaskAction(wrapper, interval, delay, runTime, maxRuns, sw, cancelToken, taskOptions);
            };
            return Task.Factory.StartNew(executor, cancelToken, taskOptions, TaskScheduler.Current);
        }
        private static void ExecutorTaskAction(Action action,
                                int interval,
                                int delay,
                                int runTime,
                                int maxRuns,
                                Stopwatch sw,
                                CancellationToken cancelToken = new CancellationToken(),
                                TaskCreationOptions taskOptions = TaskCreationOptions.None)
        {
            TaskCreationOptions taskCreationOptions = TaskCreationOptions.AttachedToParent | taskOptions;
            StopIfCancelled(cancelToken);
            if (delay > 0)
            {
                Thread.Sleep(delay);
            }
            if (maxRuns == 0) return;

            long iteration = 0;
            using (ManualResetEventSlim resetEvent = new ManualResetEventSlim(false))
            {
                while (true)
                {
                    StopIfCancelled(cancelToken);
                    Task subTask = Task.Factory.StartNew(action, cancelToken, taskCreationOptions, TaskScheduler.Current);

                    if (interval == Timeout.Infinite) { break; }

                    if (maxRuns > 0 && ++iteration >= maxRuns) { break; }

                    try
                    {
                        sw.Start();
                        resetEvent.Wait(interval, cancelToken);
                        sw.Stop();
                    }
                    finally
                    {
                        resetEvent.Reset();
                    }
                    StopIfCancelled(cancelToken);
                    if (runTime > 0 && sw.ElapsedMilliseconds >= runTime) { break; }
                }
            }
        }
        private static void StopIfCancelled(CancellationToken cancelToken)
        {
            if (cancelToken == null)
            {
                throw new ArgumentNullException("Cancellation token cannot be null");
            }
            cancelToken.ThrowIfCancellationRequested();
        }
    }
}
