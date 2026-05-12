# Native dependencies

This document describes how native binary dependencies are handled in this fork of mpv.net.

mpv.net uses native DLLs that are required at runtime. These files are not restored by `dotnet restore` and are not automatically updated by the .NET build process.

## Current native dependencies

The main native dependencies used by the Windows application are:

- `libmpv-2.dll`
- `MediaInfo.dll`

`MediaInfo.dll` is loaded directly from the application runtime path through P/Invoke in `MpvNet/Native/MediaInfo.cs`.

The release script copies `MediaInfo.dll` as an extra file during packaging. This means the DLL must already exist in the expected build output folder before the release package is created.

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
   - ARM64 for `win-arm64`, if ARM64 builds are still supported
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

The release script currently copies `MediaInfo.dll` from the build output folder as an extra file. Therefore, updating the source dependency alone is not enough if the release process uses a different output folder.

Before creating a release package, confirm that the updated `MediaInfo.dll` is present in the folders used by the release script for each architecture.

## Future improvement

A future improvement would be to automate this process with a script, for example:

```text
src/Tools/update-mediainfo.ps1
```

The script could download the official MediaInfo package, extract the required architecture-specific DLLs, place them in the correct folders, and print the detected version for validation.
