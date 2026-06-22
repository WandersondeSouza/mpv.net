
using System.Threading.Tasks;

namespace MpvNet.Help;

public static class BackgroundTaskRunner
{
    public static void Run(Action action)
    {
        Task.Run(() => {
            try
            {
                action.Invoke();
            }
            catch (Exception ex)
            {
                Terminal.WriteError(ex);
            }
        });
    }
}

[Obsolete($"Use {nameof(BackgroundTaskRunner)} instead.")]
public static class TaskHelp
{
    public static void Run(Action action) => BackgroundTaskRunner.Run(action);
}
