using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MpvNet;

internal sealed class RuntimeComponentUpdateLock : IDisposable
{
    readonly Mutex _mutex;
    bool _acquired;

    RuntimeComponentUpdateLock(Mutex mutex) => _mutex = mutex;

    public static async Task<RuntimeComponentUpdateLock> AcquireAsync(CancellationToken cancellationToken)
    {
        string userKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            Environment.UserDomainName + "\\" + Environment.UserName)))[..16];
        var result = new RuntimeComponentUpdateLock(new Mutex(false, $"Local\\mpv.net.RuntimeComponents.{userKey}"));

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (result._mutex.WaitOne(TimeSpan.FromSeconds(1)))
                    {
                        result._acquired = true;
                        return result;
                    }
                }
                catch (AbandonedMutexException)
                {
                    result._acquired = true;
                    Log.Debug("Recovered an abandoned runtime component update lock.");
                    return result;
                }

                await Task.Yield();
            }

            cancellationToken.ThrowIfCancellationRequested();
            throw new OperationCanceledException(cancellationToken);
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        try
        {
            if (_acquired)
                _mutex.ReleaseMutex();
        }
        finally
        {
            _mutex.Dispose();
        }
    }
}

internal static class RuntimeComponentStore
{
    const int PromotionAttempts = 3;

    public static void RecoverInterruptedPromotion()
    {
        if (Directory.Exists(RuntimeComponentPaths.CurrentFolder) || !Directory.Exists(RuntimeComponentPaths.PreviousFolder))
            return;

        Directory.Move(RuntimeComponentPaths.PreviousFolder, RuntimeComponentPaths.CurrentFolder);
        Log.Debug("Recovered the previous runtime component generation after an interrupted promotion.");
    }

    public static string CreateStagingSnapshot()
    {
        Directory.CreateDirectory(RuntimeComponentPaths.TempFolder);
        string staging = Path.Combine(RuntimeComponentPaths.TempFolder, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);

        string source = Directory.Exists(RuntimeComponentPaths.CurrentFolder)
            ? RuntimeComponentPaths.CurrentFolder
            : RuntimeComponentPaths.ComponentsFolder;
        if (Directory.Exists(source))
        {
            foreach (string file in Directory.GetFiles(source, "*", SearchOption.TopDirectoryOnly))
            {
                string target = Path.Combine(staging, Path.GetFileName(file));
                File.Copy(file, target, overwrite: true);
            }
        }

        return staging;
    }

    public static async Task PromoteAsync(string stagingDirectory, CancellationToken cancellationToken)
    {
        for (int attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                Promote(stagingDirectory);
                return;
            }
            catch (IOException) when (attempt < PromotionAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(150 * attempt), cancellationToken).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException) when (attempt < PromotionAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(150 * attempt), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public static void CleanupStaleStaging()
    {
        if (!Directory.Exists(RuntimeComponentPaths.TempFolder))
            return;

        foreach (string directory in Directory.GetDirectories(RuntimeComponentPaths.TempFolder))
            RuntimeComponentFileSystem.DeleteIfExists(directory);
    }

    static void Promote(string stagingDirectory)
    {
        if (!Directory.Exists(stagingDirectory))
            throw new DirectoryNotFoundException($"Runtime component staging directory was not found: {stagingDirectory}");

        RuntimeComponentFileSystem.DeleteIfExists(RuntimeComponentPaths.PreviousFolder);
        bool movedCurrent = false;
        try
        {
            if (Directory.Exists(RuntimeComponentPaths.CurrentFolder))
            {
                Directory.Move(RuntimeComponentPaths.CurrentFolder, RuntimeComponentPaths.PreviousFolder);
                movedCurrent = true;
            }

            Directory.Move(stagingDirectory, RuntimeComponentPaths.CurrentFolder);
            RuntimeComponentFileSystem.DeleteIfExists(RuntimeComponentPaths.PreviousFolder);
            Log.Debug($"Runtime component generation promoted from staging. path='{Log.SafeValue(RuntimeComponentPaths.CurrentFolder)}'");
        }
        catch
        {
            if (movedCurrent && !Directory.Exists(RuntimeComponentPaths.CurrentFolder) &&
                Directory.Exists(RuntimeComponentPaths.PreviousFolder))
            {
                Directory.Move(RuntimeComponentPaths.PreviousFolder, RuntimeComponentPaths.CurrentFolder);
            }

            throw;
        }
    }
}
