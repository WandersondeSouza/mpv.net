@{
    SchemaVersion = 1
    Source = 'shinchiro/mpv-winbuild-cmake'
    ReleaseApiUrl = 'https://api.github.com/repos/shinchiro/mpv-winbuild-cmake/releases/latest'
    Normal = @{
        FileName = 'libmpv-2.dll'
        AssetRegex = '^mpv-dev-x86_64-[0-9]{8}-git-[0-9a-z]+\.7z$'
        CachePattern = 'mpv-dev-x86_64-*.7z'
    }
    X86_64V3 = @{
        FileName = 'libmpv-2-v3.dll'
        AssetRegex = '^mpv-dev-x86_64-v3-[0-9]{8}-git-[0-9a-z]+\.7z$'
        CachePattern = 'mpv-dev-x86_64-v3-*.7z'
    }
    RequiredExports = @(
        'mpv_client_api_version'
        'mpv_create'
        'mpv_initialize'
        'mpv_command'
        'mpv_command_string'
        'mpv_get_property'
        'mpv_set_property'
        'mpv_wait_event'
        'mpv_terminate_destroy'
    )
}
