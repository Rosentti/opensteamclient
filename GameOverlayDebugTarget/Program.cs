﻿using Avalonia;
using GameOverlayUI.IPC;
using OpenSteamworks.Client.Utils.DI;
using OpenSteamworks.Structs;
using System;

namespace GameOverlayDebugTarget;

public static class Program
{
    public static Container Container { get; private set; } = new();

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        Console.CancelKeyPress += HandleCtrlC;
        AppDomain.CurrentDomain.UnhandledException += HandleUnhandledException;

        if (args.Length < 1 || !uint.TryParse(args[0], out uint gamepid))
        {
            Console.WriteLine("Not enough args, required [GamePID]");
            return;
        }

        Console.WriteLine("Game PID: " + gamepid);

        var gameidstr = Environment.GetEnvironmentVariable("SteamOverlayGameId");
        CGameID gameid;
        if (string.IsNullOrEmpty(gameidstr) || !ulong.TryParse(gameidstr, out ulong ugameid) || !(gameid = new CGameID(ugameid)).IsValid()) {
            Console.WriteLine("Invalid SteamOverlayGameId");
            return;
        }

        Console.WriteLine("GameID: " + gameid);
        Container.RegisterInstance(new SharedMemoryManager(gamepid, true));

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, Avalonia.Controls.ShutdownMode.OnExplicitShutdown);
        }
        finally
        {
            DisposeAll();
        }
    }

    private static void HandleUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        DisposeAll(); 
    }

    private static void HandleCtrlC(object? sender, ConsoleCancelEventArgs e) {
        DisposeAll();
    }

    private static bool canDispose = true;
    private static void DisposeAll() {
        if (!canDispose) {
            return;
        }

        canDispose = false;
        Container?.Get<SharedMemoryManager>().Dispose();
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>();
        return builder
                .UseSkia()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();

    }
}
