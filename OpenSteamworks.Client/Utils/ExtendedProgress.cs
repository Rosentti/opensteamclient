namespace OpenSteamworks.Client.Utils;
 
//TODO: remove this entire thing in favor of multiple IProgress instances, as part of the bootstrapper rewrite
public interface IExtendedProgress<T> : IProgress<T>
{
    /// <inheritdoc/>
    public bool Throbber { get; }
    public T InitialProgress { get; }
    public T Progress { get; }
    public T MaxProgress { get; }
    public string Operation { get; }
    public string SubOperation { get; }
    public void SetThrobber();
    public void SetProgress(T value);
    public void SetMaxProgress(T value);
    public void SetOperation(string value);
    public void SetSubOperation(string value);
    public event EventHandler<T>? ProgressChanged;
}
public class ExtendedProgress<T> : IExtendedProgress<T>
{
    /// <summary>
    /// A throbber is a progressbar that doesn't have a set max value.
    /// It is used to inform users that an operation is ongoing, but it's progression is not known.
    /// </summary>
    public bool Throbber { get; private set; }
    public T InitialProgress { get; private set; }
    public T Progress { get; private set; }
    public T MaxProgress { get; private set; }
    public string Operation { get; private set; }
    public string SubOperation { get; private set; }
    public event EventHandler<T>? ProgressChanged;
    private object PropertyLock = new object();
    private object SendLock = new object();

    private static SynchronizationContext defaultSyncContext = new SynchronizationContext();

    private readonly SynchronizationContext synchronizationContext;
    /// <summary>A cached delegate used to post invocation to the synchronization context.</summary>
    private readonly SendOrPostCallback invokeHandlers;

    public ExtendedProgress(T initialProgress, T maxProgress, string initialOperation = "")
    {
        synchronizationContext = SynchronizationContext.Current ?? defaultSyncContext;
        Operation = initialOperation;
        SubOperation = "";
        Throbber = false;
        this.InitialProgress = initialProgress;
        Progress = initialProgress;
        MaxProgress = maxProgress;
        this.invokeHandlers = new SendOrPostCallback(InvokeHandlers);
    }

    void IExtendedProgress<T>.SetThrobber() {
        lock (PropertyLock) {
            this.Throbber = true;
            (this as IProgress<T>).Report(this.InitialProgress);
        } 
    }

    void IExtendedProgress<T>.SetProgress(T value) {
        Console.WriteLine("Prog progress changed: '" + value + "'");
        lock (PropertyLock) {
            this.Throbber = false;
            (this as IProgress<T>).Report(value);
        }
    }

    void IExtendedProgress<T>.SetMaxProgress(T value) {
        Console.WriteLine("Prog max changed: '" + value + "'");
        lock (PropertyLock) {
            this.Throbber = false;
            this.MaxProgress = value;
            (this as IProgress<T>).Report(this.Progress);
        }
    }

    void IExtendedProgress<T>.SetOperation(string value) {
        Console.WriteLine("Prog operation changed: '" + value + "'");
        lock(PropertyLock) {
            this.Operation = value;
            this.SubOperation = "";
            this.Progress = this.InitialProgress;
            (this as IProgress<T>).Report(this.Progress);
        }
    }

    void IExtendedProgress<T>.SetSubOperation(string value) {
        Console.WriteLine("Prog sub operation changed: '" + value + "'");
        lock(PropertyLock) {
            this.SubOperation = value;
            (this as IProgress<T>).Report(this.Progress);
        }
    }

    void IProgress<T>.Report(T value)
    {
        // If there's no handler, don't bother going through the sync context.
        // Inside the callback, we'll need to check again, in case 
        // an event handler is removed between now and then.
        if (ProgressChanged != null)
        {
            // Post the processing to the sync context.
            // (If T is a value type, it will get boxed here.)
            synchronizationContext.Post(invokeHandlers, value);
        }
    }

    /// <summary>Invokes the action and event callbacks.</summary>
    /// <param name="state">The progress value.</param>
    private void InvokeHandlers(object? state)
    {
        lock (SendLock) {
            if (state == null) {
                return;
            }

            T value = (T)state;

            this.Progress = value;
            ProgressChanged?.Invoke(this, value);
        }
    }

}