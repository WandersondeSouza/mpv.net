using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

using MpvNet;
using MpvNet.Windows;

using Xunit;

namespace MpvNet.Tests;

public sealed class FileTypeAssociationTests
{
    const string UapNamespace = "http://schemas.microsoft.com/appx/manifest/uap/windows10";

    [Fact]
    public void StoreManifestDeclaresEveryPlayerSupportedExtensionOnce()
    {
        XDocument manifest = XDocument.Load(FindRepositoryFile("src/MpvNet.Pacote/Package.appxmanifest"));
        XNamespace uap = UapNamespace;
        XElement[] associations = manifest.Descendants(uap + "FileTypeAssociation").ToArray();
        string[] manifestExtensions = associations
            .SelectMany(association => association.Descendants(uap + "FileType"))
            .Select(fileType => fileType.Value.Trim().TrimStart('.').ToLowerInvariant())
            .ToArray();
        string[] expectedExtensions = FileTypes.DefaultVideoExts
            .Concat(FileTypes.DefaultAudioExts)
            .Concat(FileTypes.DefaultImageExts)
            .Concat(FileTypes.Playlist)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(extension => extension, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Equal(expectedExtensions, manifestExtensions
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(extension => extension, StringComparer.OrdinalIgnoreCase));
        Assert.Equal(manifestExtensions.Length, manifestExtensions.Distinct(StringComparer.OrdinalIgnoreCase).Count());

        XElement imageAssociation = Assert.Single(associations, association =>
            string.Equals((string?)association.Attribute("Name"), "mpvnetimage", StringComparison.Ordinal));
        string[] storeImageExtensions = imageAssociation.Descendants(uap + "FileType")
            .Select(fileType => fileType.Value.Trim().TrimStart('.').ToLowerInvariant())
            .OrderBy(extension => extension, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Assert.Equal(FileTypes.DefaultImageExts.OrderBy(extension => extension, StringComparer.OrdinalIgnoreCase), storeImageExtensions);
    }

    [Fact]
    public void DesktopImageRegistrationRemainsAvailableAsAnOptInAssociation()
    {
        Assert.Equal(FileTypes.GetImgExts(), FileAssociation.GetExtensionsForPerceivedType("image"));
    }

    [Fact]
    public void DesktopInstallerRegistersAudioVideoAndPlaylistsButNotImagesAutomatically()
    {
        string installer = File.ReadAllText(FindRepositoryFile("src/Setup/Inno/build-windows-installer.iss"));

        Assert.Contains("Parameters: \"--register-file-associations audio\"", installer, StringComparison.Ordinal);
        Assert.Contains("Parameters: \"--register-file-associations video\"", installer, StringComparison.Ordinal);
        Assert.Contains("Parameters: \"--register-file-associations playlist\"", installer, StringComparison.Ordinal);
        Assert.DoesNotContain("Parameters: \"--register-file-associations image\"", installer, StringComparison.Ordinal);
    }

    static string FindRepositoryFile(string relativePath)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate))
                return candidate;

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate repository file '{relativePath}' from '{AppContext.BaseDirectory}'.");
    }
}
