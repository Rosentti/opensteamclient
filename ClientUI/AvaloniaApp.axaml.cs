using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Markup.Xaml;
using ClientUI.Translation;
using ClientUI.ViewModels;
using ClientUI.Views;
using OpenSteamworks.Client.Utils;
using OpenSteamworks;
using OpenSteamworks.Client;
using OpenSteamworks.Client.Utils.Interfaces;
using OpenSteamworks.Client.Managers;
using System.Threading.Tasks;
using OpenSteamworks.Client.Config;
using OpenSteamworks.Client.CommonEventArgs;
using System;
using Avalonia.Controls;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Avalonia.Input;

namespace ClientUI;

public partial class AvaloniaApp : Application
{
    public static Container Container = new Container();
    public new static AvaloniaApp? Current;
    public new IClassicDesktopStyleApplicationLifetime ApplicationLifetime => (base.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)!;
    public static bool DebugEnabled = false;
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        this.DataContext = new AvaloniaAppViewModel();
    }

    public static void InvokeOnUIThread(Action callback) {
        if (!Container.IsShuttingDown) {
            Avalonia.Threading.Dispatcher.UIThread.Invoke(callback);
        }
    }

    private ExtendedProgress<int> loginProgress = new ExtendedProgress<int>(0, 100);
    public override async void OnFrameworkInitializationCompleted()
    {
        ExtendedProgress<int> bootstrapperProgress = new ExtendedProgress<int>(0, 100);
        var progVm = new ProgressWindowViewModel(bootstrapperProgress, "Bootstrapper progress");
        ForceProgressWindow(progVm);

        Container.RegisterInstance(new Client(Container, bootstrapperProgress));
        Container.ConstructAndRegisterImmediate<TranslationManager>();
        Container.RegisterInstance(this);
        await Container.RunClientStartup();

        // This will stay for the lifetime of the application.
        Container.Get<LoginManager>().LoggedOn += (object sender, LoggedOnEventArgs e) =>
        {
            InvokeOnUIThread(() =>
            {
                ForceMainWindow();
            });
        };

        Container.Get<LoginManager>().LogOnFailed += (object sender, LogOnFailedEventArgs e) =>
        {
            InvokeOnUIThread(() => {
                MessageBox.Show("Failed to log on", "Failed with result code: " + e.Error.ToString());
                ForceAccountPickerWindow();
            });
        };
        
        Container.Get<LoginManager>().LoggedOff += (object sender, LoggedOffEventArgs e) =>
        {
            InvokeOnUIThread(() => {
                if (e.Error != null && e.Error.Value != OpenSteamworks.Enums.EResult.k_EResultOK) {
                    // What can cause a sudden log off?
                    MessageBox.Show("Session terminated", "You were forcibly logged off with an error code: " + e.Error.ToString());
                }
                ForceAccountPickerWindow();
            });
        };

        // This is kept for the lifetime of the application, which is fine
        Container.Get<LoginManager>().SetProgress(loginProgress);
        Container.Get<LoginManager>().SetExceptionHandler(e => MessageBox.Error(e));
        Container.Get<LoginManager>().LogonStarted += (object sender, EventArgs e) =>
        {
            InvokeOnUIThread(() =>
            {
                this.ForceProgressWindow(new ProgressWindowViewModel(loginProgress, "Login progress"));
            });
        };

        Container.Get<LoginManager>().LoggingOff += (object? sender, EventArgs e) =>
        {
            InvokeOnUIThread(() =>
            {
                this.ForceProgressWindow(new ProgressWindowViewModel(loginProgress, "Logout progress"));
            });
        };


        if (!Container.Get<LoginManager>().TryAutologin()) {
            ForceAccountPickerWindow();
        }

        base.OnFrameworkInitializationCompleted();
        var icons = TrayIcon.GetIcons(this);
        UtilityFunctions.AssertNotNull(icons);
        foreach (var icon in icons)
        {
            Container.Get<TranslationManager>().TranslateTrayIcon(icon);
        }
    }

    /// <summary>
    /// Closes the current MainWindow (if exists) and replaces it with the account picker
    /// </summary>
    public void ForceAccountPickerWindow() {
        ForceWindow(new AccountPickerWindow
        {
            DataContext = AvaloniaApp.Container.ConstructOnly<AccountPickerWindowViewModel>()
        });
    }

    /// <summary>
    /// Closes the current MainWindow (if exists) and replaces it with a new MainWindow
    /// </summary>
    public void ForceMainWindow() {
        ForceWindow(new MainWindow
        {
            DataContext = AvaloniaApp.Container.ConstructOnly<MainWindowViewModel>(new object[] { (Action)OpenSettingsWindow })
        });
    }

    private SettingsWindow? CurrentSettingsWindow;
    public void OpenSettingsWindow() {
        if (CurrentSettingsWindow != null) {
            if (CurrentSettingsWindow.PlatformImpl != null) {
                // Not closed but maybe hidden, maybe shown in background
                CurrentSettingsWindow.Show();
                CurrentSettingsWindow.Activate();
                return;
            }
        }

        CurrentSettingsWindow = new()
        {
            DataContext = AvaloniaApp.Container.ConstructOnly<SettingsWindowViewModel>()
        };

        CurrentSettingsWindow.Show();
        CurrentSettingsWindow.Closed += (object? sender, EventArgs e) =>
        {
            CurrentSettingsWindow = null;
        };
    }

    private InterfaceList? CurrentInterfaceListWindow;
    public void OpenInterfaceList() {
        if (CurrentInterfaceListWindow != null) {
            if (CurrentInterfaceListWindow.PlatformImpl != null) {
                // Not closed but maybe hidden, maybe shown in background
                CurrentInterfaceListWindow.Show();
                CurrentInterfaceListWindow.Activate();
                return;
            }
        }

        CurrentInterfaceListWindow = new();

        CurrentInterfaceListWindow.Show();
        CurrentInterfaceListWindow.Closed += (object? sender, EventArgs e) =>
        {
            CurrentInterfaceListWindow = null;
        };
    }

    public void OpenMainWindow() {
        if (ApplicationLifetime.MainWindow != null) {
            if (ApplicationLifetime.MainWindow.IsActive) {
                // Not closed but maybe hidden, maybe shown in background
                ApplicationLifetime.MainWindow.Show();
                ApplicationLifetime.MainWindow.Activate();
                return;
            }
        }

        ForceMainWindow();
    }

    /// <summary>
    /// Closes the current MainWindow (if exists) and replaces it with the login screen
    /// </summary>
    public void ForceLoginWindow(LoginUser? user) {
        LoginWindowViewModel vm;
        if (user == null) {
            vm = AvaloniaApp.Container.ConstructOnly<LoginWindowViewModel>();
        } else {
            vm = AvaloniaApp.Container.ConstructOnly<LoginWindowViewModel>(new object[1] { user });
        }

        var window = new LoginWindow();
        //TODO: hacky
        vm.ShowSecondFactorDialog = window.ShowSecondFactorDialog;
        window.DataContext = vm;

        ForceWindow(window);
    }

    /// <summary>
    /// Closes the current MainWindow (if exists) and replaces it with a new Progress Window, with the user specified ProgressWindowViewModel
    /// </summary>
    public void ForceProgressWindow(ProgressWindowViewModel progVm) {
        var progressWindow = new ProgressWindow(progVm);
        ForceWindow(new ProgressWindow(progVm));
    }

    /// <summary>
    /// Closes the current MainWindow (if exists) and replaces it with a user specified window
    /// </summary>
    public void ForceWindow(Window window) {
        if (!Container.IsShuttingDown) {
            ApplicationLifetime.MainWindow?.Close();
            ApplicationLifetime.MainWindow = window;
            ApplicationLifetime.MainWindow.Show();
        }
    }

    public AvaloniaApp()
    {
        Current = this;
    }

    /// <summary>
    /// Async exit function. Will hang in certain cases for some unknown reason.
    /// </summary>
    public async Task Exit(int exitCode = 0) {
        await Container.RunClientShutdown();
        Console.WriteLine("Shutting down Avalonia");
        ApplicationLifetime.Shutdown(exitCode);
    }

    /// <summary>
    /// A synchronous exit function. Simply calls Task.Run. 
    /// </summary>
    public void ExitEventually(int exitCode = 0) {
        Task.Run(async () => await this.Exit(exitCode));
    }
}