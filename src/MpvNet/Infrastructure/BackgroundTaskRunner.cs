
using System.Threading;
using System.Threading.Tasks;

namespace MpvNet.Help;

public static class BackgroundTaskRunner
{
    public static void Run(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        Task.Run(() => {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Terminal.WriteError(ex);
            }
        });
    }

    public static void Run(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default,
        Action<Exception>? exceptionHandler = null)
    {
        ArgumentNullException.ThrowIfNull(operation);
        Task backgroundTask = RunAsync(operation, cancellationToken, exceptionHandler);
    }

    static async Task RunAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken,
        Action<Exception>? exceptionHandler)
    {
        try
        {
            await operation(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            if (exceptionHandler is not null)
                exceptionHandler(ex);
            else
                Terminal.WriteError(ex);
        }
    }
}

[Obsolete($"Use {nameof(BackgroundTaskRunner)} instead.")]
public static class TaskHelp
{
    public static void Run(Action action) => BackgroundTaskRunner.Run(action);
}
