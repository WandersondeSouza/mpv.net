
using System.Threading.Tasks;

namespace MpvNet.Help;

public static class TaskHelp
{
    public static void Run(Action action)
    {
        Task.Run(() => {
            try
            {
                action.Invoke();
            }
            catch (Exception e)
            {
                Terminal.WriteError(e);
            }
        });
    }
}
