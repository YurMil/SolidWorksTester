using System;
using System.Threading;
using System.Threading.Tasks;

namespace SolidWorksTester.Services
{
    /// <summary>Runs SOLIDWORKS COM work on a dedicated STA thread (required by COM).</summary>
    internal static class StaTaskRunner
    {
        public static Task Run(Action action, CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var thread = new Thread(() =>
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    action();
                    cancellationToken.ThrowIfCancellationRequested();
                    tcs.TrySetResult();
                }
                catch (OperationCanceledException)
                {
                    tcs.TrySetCanceled(cancellationToken);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            })
            {
                IsBackground = true,
                Name = "SolidWorksStaWorker"
            };

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return tcs.Task;
        }
    }
}
