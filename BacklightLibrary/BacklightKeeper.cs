using BacklightLibrary.Events;

namespace BacklightLibrary;

public sealed class BacklightKeeper
{
    private const string BacklightKeeperMutexName = "ThinkpadBacklightControlMutex";
    private readonly Mutex _backlightOpsMutex;
    private readonly Backlight _controller;
    private readonly object _exitLoopLock = new();
    private readonly Thread _mainThread;
    private bool _exitLoop;
    private int _loopInterval = 250;
    private BacklightState _targetState;

    /// <summary>
    ///     Create instance of this class consistently forcing a specified keyboard backlight state.
    /// </summary>
    /// <param name="targetState">The backlight state which is the target to keep.</param>
    /// <exception cref="Exception"></exception>
    public BacklightKeeper(BacklightState targetState)
    {
        _controller = new Backlight();
        _targetState = targetState;
        _backlightOpsMutex = new Mutex(false, BacklightKeeperMutexName, out var isNew);
        if (!isNew) throw new Exception("Creation of resource lock failed: Mutex already exists.");
        _mainThread = new Thread(MainLoop);
        _controller.OnChanged += (_, _) => { AssignTargetState(); };
    }

    public BacklightKeeper(ref Backlight controller, BacklightState targetState)
    {
        _controller = controller;
        _targetState = targetState;
        _backlightOpsMutex = new Mutex(false, BacklightKeeperMutexName, out var isNew);
        if (!isNew) throw new Exception("Creation of resource lock failed: Mutex already exists.");
        _mainThread = new Thread(MainLoop);
        controller.OnChanged += (_, _) => { AssignTargetState(); };
    }

    /// <summary>
    ///     Get or ser the main loop interval in milliseconds. Minimum is 100 ms.
    /// </summary>
    public int LoopInterval
    {
        get => _loopInterval;
        set => _loopInterval = value < 100 ? 100 : value;
    }

    public BacklightState TargetState
    {
        get => _targetState;
        set
        {
            _backlightOpsMutex.WaitOne();
            _targetState = value;
            _backlightOpsMutex.ReleaseMutex();
        }
    }

    public event ExceptionEventHandler? OnException;

    public void Start()
    {
        _controller.Enabled = true;
        _mainThread.Start();
    }

    public void Stop()
    {
        lock (_exitLoopLock)
        {
            _exitLoop = true;
        }

        _mainThread.Join();
        _controller.Stop();
    }

    private void MainLoop(object? obj)
    {
        while (true)
        {
            lock (_exitLoopLock)
            {
                if (_exitLoop) break;
            }

            Thread.Sleep(_loopInterval);
            AssignTargetState();
        }
    }

    private void AssignTargetState()
    {
        _backlightOpsMutex.WaitOne();
        try
        {
            if ((BacklightState)_controller.ReadState() == _targetState)
            {
                _backlightOpsMutex.ReleaseMutex();
                return;
            }

            Thread.Sleep(_loopInterval);
            _controller.ChangeState((int)_targetState);
            _backlightOpsMutex.ReleaseMutex();
        }
        catch (Exception ex)
        {
            _backlightOpsMutex.ReleaseMutex();
            var invokeThread = new Thread(() => { OnException.SafeInvoke(this, new ExceptionEventArgs(ex)); });
            invokeThread.Start();
        }
    }
}

public enum BacklightState
{
    Off = 0,
    Dim = 1,
    Full = 2
}