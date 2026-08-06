using System.Buffers.Binary;

namespace MpvNet;

internal static class RuntimeComponentValidator
{
    const ushort ImageFileMachineAmd64 = 0x8664;

    public static ComponentValidationResult Validate(string componentName, string path)
    {
        try
        {
            FileInfo file = new(path);
            if (!file.Exists)
                return new(false, null, "The file does not exist.");

            if (file.Length <= 0)
                return new(false, null, "The file is empty.");

            if (RequiresPeValidation(componentName) && !IsX64PortableExecutable(path, out string? message))
                return new(false, null, message);

            string? version = null;
            try
            {
                version = FileVersionInfo.GetVersionInfo(path).FileVersion;
            }
            catch (Exception ex) when (ex is ArgumentException or FileNotFoundException)
            {
                // Version information is optional for third-party executables.
            }

            return new(true, version, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return new(false, null, ex.Message);
        }
    }

    public static bool IsX64PortableExecutable(string path, out string? diagnosticMessage)
    {
        diagnosticMessage = null;
        try
        {
            using FileStream stream = File.OpenRead(path);
            if (stream.Length < 64)
            {
                diagnosticMessage = "The file is too small to be a PE executable.";
                return false;
            }

            Span<byte> dosHeader = stackalloc byte[64];
            if (stream.Read(dosHeader) != dosHeader.Length || dosHeader[0] != (byte)'M' || dosHeader[1] != (byte)'Z')
            {
                diagnosticMessage = "The file does not have a DOS/PE header.";
                return false;
            }

            int peOffset = BinaryPrimitives.ReadInt32LittleEndian(dosHeader[60..]);
            if (peOffset < dosHeader.Length || peOffset > stream.Length - 6)
            {
                diagnosticMessage = "The PE header offset is invalid.";
                return false;
            }

            stream.Position = peOffset;
            Span<byte> peHeader = stackalloc byte[6];
            if (stream.Read(peHeader) != peHeader.Length ||
                peHeader[0] != (byte)'P' || peHeader[1] != (byte)'E' || peHeader[2] != 0 || peHeader[3] != 0)
            {
                diagnosticMessage = "The PE signature is invalid.";
                return false;
            }

            ushort machine = BinaryPrimitives.ReadUInt16LittleEndian(peHeader[4..]);
            if (machine != ImageFileMachineAmd64)
            {
                diagnosticMessage = $"The executable architecture is 0x{machine:X4}; x64 is required.";
                return false;
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            diagnosticMessage = ex.Message;
            return false;
        }
    }

    static bool RequiresPeValidation(string componentName) =>
        componentName.Equals("ffmpeg.exe", StringComparison.OrdinalIgnoreCase) ||
        componentName.Equals("ffplay.exe", StringComparison.OrdinalIgnoreCase) ||
        componentName.Equals("ffprobe.exe", StringComparison.OrdinalIgnoreCase) ||
        componentName.Equals("yt-dlp.exe", StringComparison.OrdinalIgnoreCase) ||
        componentName.Equals("mpvnet.com", StringComparison.OrdinalIgnoreCase);
}
