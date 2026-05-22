# Native dependencies

This document describes how native binary dependencies are handled in this fork of mpv.net.

mpv.net uses native DLLs that are required at runtime. Some are restored by the .NET SDK during self-contained publish; other binaries must be downloaded by the release scripts from their official upstream sources.

## Current native dependencies

The main native dependencies used by the Windows application are:

- `libmpv-2.dll`
- `MediaInfo.dll`
- `D3DCompiler_47_cor3.dll`
- `vcruntime140_cor3.dll`
- `wpfgfx_cor3.dll`
- `PenImc_cor3.dll`
- `PresentationNative_cor3.dll`

`libmpv-2.dll` is loaded directly through P/Invoke in `MpvNet/Native/LibMpv.cs`.
`MediaInfo.dll` is loaded directly through P/Invoke in `MpvNet/Native/MediaInfo.cs`.

Because both declarations use only the DLL file name, the DLLs must be available in the runtime search path. For the portable package, keep them in the same folder as `mpvnet.exe`.

The portable package also includes auxiliary executables expected beside `mpvnet.exe`:

- `ffmpeg.exe`
- `ffplay.exe`
- `ffprobe.exe`
- `yt-dlp.exe`

These executables are not called directly by the C# P/Invoke layer. They are auxiliary tools used by mpv/libmpv and streaming workflows. `yt-dlp.exe` can also be found through `PATH`, but the fork's portable package downloads and includes it beside `mpvnet.exe`.

The release script downloads FFmpeg, libmpv, yt-dlp and MediaInfo during packaging. Microsoft .NET/WPF native DLLs are not downloaded manually; they come from `dotnet publish --self-contained true --runtime win-x64`.

## Automatically downloaded release dependencies

`src/Tools/release-mpv.net.ps1` downloads or validates these dependencies before creating the portable ZIP:

- FFmpeg from `https://api.github.com/repos/BtbN/FFmpeg-Builds/releases/latest`, selecting `ffmpeg-master-latest-win64-gpl.zip` and copying only `ffmpeg.exe`, `ffplay.exe` and `ffprobe.exe`.
- libmpv from `https://api.github.com/repos/shinchiro/mpv-winbuild-cmake/releases/latest`, selecting the generic x64 asset `mpv-dev-x86_64-[date]-git-[hash].7z` and copying only `libmpv-2.dll`.
- yt-dlp from `https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe`.
- MediaInfo from the official MediaArea Windows download page `https://mediaarea.net/en/MediaInfo/Download/Windows`, selecting `MediaInfo_DLL_[version]_Windows_x64_WithoutInstaller.7z`.
- Gettext.Tools from `https://api.nuget.org/v3-flatcontainer/gettext.tools/` when `msgfmt.exe` is not available on `PATH`, so the release script can generate `Locale` from `lang/po`.
- `D3DCompiler_47_cor3.dll`, `vcruntime140_cor3.dll`, `wpfgfx_cor3.dll`, `PenImc_cor3.dll` and `PresentationNative_cor3.dll` from the official self-contained .NET Desktop/WPF publish output.

The libmpv selector intentionally uses the generic `x86_64` asset instead of `x86_64-v3` to preserve compatibility with more x64 CPUs. If an upstream release changes asset names, if a download fails, if extraction fails, or if a required downloaded file is empty, the script must fail before creating a partial portable package.

## MediaInfo.dll

`MediaInfo.dll` is part of the MediaInfo/MediaInfoLib project and is used to read technical metadata from media files, such as video, audio, subtitle, and general stream information.

The application expects the DLL name to remain:

```text
MediaInfo.dll
```

Do not rename the file unless the P/Invoke declarations in `MpvNet/Native/MediaInfo.cs` are also updated.

## How to prepare local native dependencies

Preparing the local output folder is automated by `src/Tools/ensure-native-dependencies.ps1`.

Default process:

```powershell
src\Tools\ensure-native-dependencies.ps1 -SourceDir .\src -TargetDir .\src\MpvNet.Windows\bin\Debug\win-x64
```

The same preparation can be requested during a Debug build:

```powershell
dotnet build src\MpvNet.Windows\MpvNet.Windows.csproj /p:EnsureNativeDependencies=true
```

To pin a specific MediaInfo version, pass `-MediaInfoVersion` or set `MPVNET_MEDIAINFO_VERSION`:

```powershell
src\Tools\ensure-native-dependencies.ps1 -SourceDir .\src -TargetDir .\src\MpvNet.Windows\bin\Debug\win-x64 -MediaInfoVersion 26.05
```

The script stores downloaded files under `artifacts/native-dependencies`, downloads missing FFmpeg/libmpv/yt-dlp/MediaInfo binaries, verifies that native binaries are non-empty x64 PE files, and can copy/validate the required .NET/WPF native DLLs from a self-contained publish output when `-PublishDir` is provided. Pass `-UpdateExisting` to refresh files that already exist. Microsoft .NET/WPF DLLs are not downloaded manually.

Suggested test files:

- A normal MP4 file with one video and one audio stream.
- A video file with multiple audio tracks.
- A video file with embedded subtitles.
- A file with incomplete or unusual metadata.

## API compatibility checklist

After replacing `MediaInfo.dll`, verify that the exported C API functions used by the application still work:

- `MediaInfo_New`
- `MediaInfo_Open`
- `MediaInfo_Option`
- `MediaInfo_Inform`
- `MediaInfo_Get`
- `MediaInfo_Count_Get`
- `MediaInfo_Close`
- `MediaInfo_Delete`

These functions are declared in `MpvNet/Native/MediaInfo.cs`.

## Validation after update

After updating the DLL, run the application and check the following behavior:

- mpv.net starts without native DLL load errors.
- Media information can be opened for common video files.
- General metadata is displayed correctly.
- Video stream information is displayed correctly.
- Audio stream information is displayed correctly.
- Subtitle/text stream information is displayed correctly when available.

If the application fails to start or media information cannot be loaded, confirm that:

- The DLL architecture matches the application runtime architecture.
- The file is named exactly `MediaInfo.dll`.
- The DLL is located in the same runtime folder expected by the application.
- Required runtime dependencies of MediaInfo are present.

## Release packaging note

The release script publishes self-contained `win-x64`, calls `ensure-native-dependencies.ps1 -UpdateExisting`, places the required extra files in the x64 build output folder, and copies them for the portable and publish folders. `mpvnet.com` can be provided by `-MpvNetComFile`; otherwise the script downloads the upstream helper file when it is not already present in the build output.

Before creating a release package, validate the final folder or ZIP:

```powershell
src\Tools\test-native-dependencies.ps1 -Path .\src\MpvNet.Windows\bin\Debug\win-x64\publish
src\Tools\test-native-dependencies.ps1 -ZipFile .\artifacts\release\mpv.net-v7.1.2.2-portable-x64.zip
```

If any expected file is missing, empty, not x64, or cannot be downloaded, the release script fails instead of creating a partial portable package. Do not download DLLs from generic DLL websites such as dll-files.com.
