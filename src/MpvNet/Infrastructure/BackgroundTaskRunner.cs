
using System.Threading;
using System.Threading.Tasks;

namespace MpvNet.Help;

public static class BackgroundTaskRunner
{
    public static void Run(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        // Deliberate fire-and-forget: RunAsync observes cancellation and every exception.
        Task ignoredTask = RunAsync(_ =>
        {
            action();
            return Task.CompletedTask;
        });
    }

    public static void Run(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default,
        Action<Exception>? exceptionHandler = null)
    {
        ArgumentNullException.ThrowIfNull(operation);
        // Deliberate fire-and-forget: RunAsync observes cancellation and every exception.
        Task ignoredTask = RunAsync(operation, cancellationToken, exceptionHandler);
    }

    public static async Task RunAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default,
        Action<Exception>? exceptionHandler = null)
    {
        ArgumentNullException.ThrowIfNull(operation);

        try
        {
            await Task.Run(() => operation(cancellationToken), cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            try
            {
                if (exceptionHandler is not null)
                    exceptionHandler(ex);
                else
                    Terminal.WriteError(ex);
            }
            catch (Exception handlerException)
            {
                Terminal.WriteError(handlerException);
            }
        }
    }
}

[Obsolete($"Use {nameof(BackgroundTaskRunner)} instead.")]
public static class TaskHelp
{
    public static void Run(Action action) => BackgroundTaskRunner.Run(action);
}
