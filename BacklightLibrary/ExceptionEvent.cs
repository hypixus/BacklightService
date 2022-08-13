using System.Runtime.CompilerServices;

namespace BacklightLibrary;

public class ExceptionEventArgs : EventArgs
{
    public ExceptionEventArgs(Exception exception)
    {
        Exception = exception;
    }

    /// <summary>
    ///     The new state of the backlight after the event (ex. 0/1/2)
    /// </summary>
    public Exception Exception { get; }
}

public delegate void ExceptionEventHandler(object sender, ExceptionEventArgs e);