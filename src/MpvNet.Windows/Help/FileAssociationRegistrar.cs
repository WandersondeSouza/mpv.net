namespace MpvNet.Windows.Help;

public static class FileAssociationRegistrar
{
    public static int RegisterElevated(string perceivedType, string[] extensions)
    {
        using Process proc = new Process();
        proc.StartInfo.FileName = Environment.ProcessPath;
        proc.StartInfo.Arguments = "--register-file-associations " +
            perceivedType + " " + string.Join(" ", extensions);
        proc.StartInfo.Verb = "runas";
        proc.StartInfo.UseShellExecute = true;
        proc.Start();
        proc.WaitForExit();

        return proc.ExitCode;
    }
}
