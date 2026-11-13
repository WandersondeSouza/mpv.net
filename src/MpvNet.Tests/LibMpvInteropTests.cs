using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using MpvNet.Native;

using Xunit;

namespace MpvNet.Tests;

public sealed class LibMpvInteropTests
{
    [Fact]
    public void EventStructuresUseTheNativePointerLayout()
    {
        Assert.Equal(IntPtr.Size == 8 ? 24 : 20, Marshal.SizeOf<LibMpv.mpv_event>());
        Assert.Equal(IntPtr.Size == 8 ? 24 : 12, Marshal.SizeOf<LibMpv.mpv_event_property>());
        Assert.Equal((IntPtr)0, Marshal.OffsetOf<LibMpv.mpv_event_property>(nameof(LibMpv.mpv_event_property.name)));
        Assert.Equal((IntPtr)IntPtr.Size, Marshal.OffsetOf<LibMpv.mpv_event_property>(nameof(LibMpv.mpv_event_property.format)));
        Assert.Equal((IntPtr)(IntPtr.Size * 2), Marshal.OffsetOf<LibMpv.mpv_event_property>(nameof(LibMpv.mpv_event_property.data)));
    }

    [Fact]
    public void EventPropertyNameIsDecodedAsUtf8()
    {
        byte[] name = System.Text.Encoding.UTF8.GetBytes("áudio\0");
        nint pointer = Marshal.AllocHGlobal(name.Length);
        try
        {
            Marshal.Copy(name, 0, pointer, name.Length);
            LibMpv.mpv_event_property property = new() { name = pointer };

            Assert.Equal("áudio", LibMpv.ConvertFromUtf8(property.name));
        }
        finally
        {
            Marshal.FreeHGlobal(pointer);
        }
    }

    [Fact]
    public void Utf8DecodingPreservesNullTerminationAndInvalidByteReplacement()
    {
        Assert.Empty(LibMpv.ConvertFromUtf8(IntPtr.Zero));

        byte[] bytes = [0xF0, 0x9F, 0x8E, 0xAC, 0xFF, 0, 0x41, 0];
        nint pointer = Marshal.AllocHGlobal(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, pointer, bytes.Length);
            Assert.Equal("\U0001F3AC\uFFFD", LibMpv.ConvertFromUtf8(pointer));
            Assert.Empty(LibMpv.ConvertFromUtf8(pointer + 5));
        }
        finally
        {
            Marshal.FreeHGlobal(pointer);
        }
    }

    [Fact]
    public void NativePropertyReadersPreserveUtf8AndUnavailableValues()
    {
        RuntimeComponents.RegisterNativeResolver();
        using MainPlayer player = new();
        player.MainHandle = LibMpv.mpv_create();
        Assert.NotEqual(IntPtr.Zero, player.MainHandle);
        foreach ((string option, string setting) in new[] { ("config", "no"), ("idle", "yes"), ("load-scripts", "no") })
            Assert.Equal(0, LibMpv.mpv_set_option_string(
                player.MainHandle, LibMpv.GetUtf8Bytes(option), LibMpv.GetUtf8Bytes(setting)));
        Assert.Equal(LibMpv.mpv_error.MPV_ERROR_SUCCESS, LibMpv.mpv_initialize(player.MainHandle));
        player.SetMpvInitialized();
        player.Handle = LibMpv.mpv_create_client(player.MainHandle, "property-tests");
        Assert.NotEqual(IntPtr.Zero, player.Handle);

        const string property = "user-data/mpvnet-test-title";
        const string value = "Título em português";

        Assert.Empty(player.GetPropertyString(property));
        Assert.Empty(player.GetPropertyOsdString(property));
        Assert.False(player.TryGetPropertyString(property, out string missing));
        Assert.Empty(missing);

        player.SetPropertyString(property, value);

        // libmpv serializes user-data as JSON for MPV_FORMAT_STRING.
        string json = $"\"{value}\"";
        Assert.Equal(json, player.GetPropertyString(property));
        Assert.Equal(value, player.GetPropertyOsdString(property));
        Assert.True(player.TryGetPropertyString(property, out string actual));
        Assert.Equal(json, actual);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DestroyReleasesMainHandleSharedDuringPartialInitialization(bool initialized)
    {
        RuntimeComponents.RegisterNativeResolver();
        using MainPlayer player = new();
        player.MainHandle = LibMpv.mpv_create();
        Assert.NotEqual(IntPtr.Zero, player.MainHandle);
        player.Handle = player.MainHandle;

        if (initialized)
        {
            player.SetOptionString("config", "no");
            player.SetOptionString("load-scripts", "no");
            Assert.Equal(LibMpv.mpv_error.MPV_ERROR_SUCCESS, LibMpv.mpv_initialize(player.MainHandle));
            player.SetMpvInitialized();
        }

        player.Destroy();
        player.Destroy();

        Assert.Equal(IntPtr.Zero, player.Handle);
        Assert.Equal(IntPtr.Zero, player.MainHandle);
        Assert.Equal(PlayerLifecycleState.Destroyed, player.LifecycleState);
    }

    [Fact]
    public void CreateNewPlayerRequiresAMainContextAndRejectsDisposedPlayer()
    {
        using MainPlayer player = new();

        Assert.Throws<InvalidOperationException>(() => player.CreateNewPlayer("before_startup"));
        player.Destroy();
        Assert.Throws<ObjectDisposedException>(() => player.CreateNewPlayer("after_shutdown"));
        Assert.Empty(player.Clients);
    }

    [Fact]
    public async Task DestroyWaitsForExtensionClientLoopAndRejectsNewClients()
    {
        RuntimeComponents.RegisterNativeResolver();
        using MainPlayer player = new();
        player.MainHandle = LibMpv.mpv_create();
        Assert.NotEqual(IntPtr.Zero, player.MainHandle);
        player.Handle = player.MainHandle;
        player.SetOptionString("config", "no");
        player.SetOptionString("load-scripts", "no");
        player.SetOptionString("idle", "yes");
        Assert.Equal(LibMpv.mpv_error.MPV_ERROR_SUCCESS, LibMpv.mpv_initialize(player.MainHandle));
        player.SetMpvInitialized();

        var callbackStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCallback = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var shutdownStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenRegistration registration = player.PlayerCancellationToken.Register(
            () => shutdownStarted.TrySetResult());
        TimeSpan timeout = TimeSpan.FromSeconds(10);
        CancellationToken testCancellation = TestContext.Current.CancellationToken;
        MpvClient client = player.CreateNewPlayer("extension");
        Assert.Same(client, Assert.Single(player.Clients));
        client.ObservePropertyBool("pause", _ =>
        {
            callbackStarted.TrySetResult();
            releaseCallback.Task.GetAwaiter().GetResult();
        });

        Task? destroyTask = null;
        try
        {
            await callbackStarted.Task.WaitAsync(timeout, testCancellation);
            destroyTask = Task.Run(player.Destroy, testCancellation);
            await shutdownStarted.Task.WaitAsync(timeout, testCancellation);
            Assert.False(destroyTask.IsCompleted);
            Assert.NotEqual(IntPtr.Zero, player.MainHandle);
            Task createTask = Task.Run(() => Assert.Throws<ObjectDisposedException>(() =>
            {
                using MpvClient lateClient = player.CreateNewPlayer("during_shutdown");
            }), testCancellation);
            await createTask.WaitAsync(timeout, testCancellation);
        }
        finally
        {
            releaseCallback.TrySetResult();
            if (destroyTask is not null)
                await destroyTask.WaitAsync(timeout, testCancellation);
        }

        Assert.Empty(player.Clients);
        Assert.Equal(IntPtr.Zero, client.Handle);
        Assert.Equal(IntPtr.Zero, player.MainHandle);
    }

    [Fact]
    public void UnmanagedStringArrayWritesUtf8PointersAndNullSentinel()
    {
        using UnmanagedStringArray values = new(new[] { "comando", "áudio" });

        Assert.Equal("comando", LibMpv.ConvertFromUtf8(Marshal.ReadIntPtr(values.Pointer, 0)));
        Assert.Equal("áudio", LibMpv.ConvertFromUtf8(Marshal.ReadIntPtr(values.Pointer, IntPtr.Size)));
        Assert.Equal(IntPtr.Zero, Marshal.ReadIntPtr(values.Pointer, IntPtr.Size * 2));
    }

    [Fact]
    public void UnmanagedStringArrayDisposeIsIdempotent()
    {
        UnmanagedStringArray values = new(new[] { "comando" });

        values.Dispose();
        values.Dispose();

        Assert.Equal(IntPtr.Zero, values.Pointer);
    }
}
