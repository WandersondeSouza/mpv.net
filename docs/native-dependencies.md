# Native dependencies

This document describes how native binary dependencies are handled in this fork of mpv.net.

mpv.net uses native DLLs that are required at runtime. These files are not restored by `dotnet restore` and are not automatically updated by the .NET build process.

## Current native dependencies

The main native dependencies used by the Windows application are:

- `libmpv-2.dll`
- `MediaInfo.dll`

`libmpv-2.dll` is loaded directly through P/Invoke in `MpvNet/Native/LibMpv.cs`.
`MediaInfo.dll` is loaded directly through P/Invoke in `MpvNet/Native/MediaInfo.cs`.

Because both declarations use only the DLL file name, the DLLs must be available in the runtime search path. For the portable package, keep them in the same folder as `mpvnet.exe`.

The portable package also includes auxiliary executables expected beside `mpvnet.exe`:

- `ffmpeg.exe`
- `ffplay.exe`
- `ffprobe.exe`
- `yt-dlp.exe`

These executables are not called directly by the C# P/Invoke layer. They are auxiliary tools used by mpv/libmpv and streaming workflows. `yt-dlp.exe` can also be found through `PATH`, but the fork's portable package downloads and includes it beside `mpvnet.exe`.

The release script downloads FFmpeg, libmpv and yt-dlp during packaging. `MediaInfo.dll` remains a manual/local dependency and must already exist in the expected build output folder before the release package is created.

## Automatically downloaded release dependencies

`src/Tools/release-mpv.net.ps1` downloads these dependencies before creating the portable ZIP:

- FFmpeg from `https://api.github.com/repos/BtbN/FFmpeg-Builds/releases/latest`, selecting `ffmpeg-master-latest-win64-gpl.zip` and copying only `ffmpeg.exe`, `ffplay.exe` and `ffprobe.exe`.
- libmpv from `https://api.github.com/repos/shinchiro/mpv-winbuild-cmake/releases/latest`, selecting the generic x64 asset `mpv-dev-x86_64-[date]-git-[hash].7z` and copying only `libmpv-2.dll`.
- yt-dlp from `https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe`.

The libmpv selector intentionally uses the generic `x86_64` asset instead of `x86_64-v3` to preserve compatibility with more x64 CPUs. If an upstream release changes asset names, if a download fails, if extraction fails, or if a required downloaded file is empty, the script must fail before creating a partial portable package.

## MediaInfo.dll

`MediaInfo.dll` is part of the MediaInfo/MediaInfoLib project and is used to read technical metadata from media files, such as video, audio, subtitle, and general stream information.

The application expects the DLL name to remain:

```text
MediaInfo.dll
```

Do not rename the file unless the P/Invoke declarations in `MpvNet/Native/MediaInfo.cs` are also updated.

## How to update MediaInfo.dll

Updating `MediaInfo.dll` is currently a manual maintenance task.

Recommended process:

1. Download the latest official MediaInfo/MediaInfoLib package for Windows from the official MediaInfo source.
2. Select the correct architecture version:
   - x64 for `win-x64`
3. Replace the existing `MediaInfo.dll` in the corresponding dependency/build output location.
4. Build and run mpv.net.
5. Test media information loading with different file types.

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

The release script downloads `libmpv-2.dll`, `ffmpeg.exe`, `ffplay.exe`, `ffprobe.exe` and `yt-dlp.exe`, places them in the x64 build output folder, and then copies them as extra files for the portable and publish folders. `MediaInfo.dll` and `mpvnet.com` are still copied from the local build output folder as required files.

Before creating a release package, confirm that the local required files are present in the folder used by the release script:

```text
src/MpvNet.Windows/bin/Debug/win-x64/
```

If any expected file is missing, empty, or cannot be downloaded, the release script should fail instead of creating a partial portable package.

## Future improvement

A future improvement would be to automate this process with a script, for example:

```text
src/Tools/update-mediainfo.ps1
```

The script could download the official MediaInfo package, extract the required architecture-specific DLLs, place them in the correct folders, and print the detected version for validation.
