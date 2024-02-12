﻿using Avalonia;
using System;
using System.Linq;
using System.Security.Principal;

namespace Installer;

public static class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
#if DEBUG
        if (!OperatingSystem.IsWindows()) {
            AvaloniaApp.InLinuxDevelopment = true;
        }
#endif

        if (args.Contains("-debug"))
        {
            AvaloniaApp.DebugEnabled = true;
        }
#if DEBUG
        Console.WriteLine("Running DEBUG build, debug mode forced on");
        AvaloniaApp.DebugEnabled = true;
#endif

        if (!AvaloniaApp.InLinuxDevelopment) {
            if (!IsAdministrator()) {
                return;
            }
        } 

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, Avalonia.Controls.ShutdownMode.OnMainWindowClose);
    }

    public static bool IsAdministrator()
    {
        var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<AvaloniaApp>()
            .UsePlatformDetect()
            .WithInterFont()
            .UseSkia()
            .LogToTrace();
}