namespace BacklightLibrary;

/// <summary>
///     Event args used for <see cref="BacklightEventHandler" />
/// </summary>
public class BacklightEventArgs : EventArgs
{
    public BacklightEventArgs(int state)
    {
        State = state;
    }

    /// <summary>
    ///     The new state of the backlight after the event (ex. 0/1/2)
    /// </summary>
    public int State { get; }
}


/// <summary>
///     Event handler for processing a change in backlight state as in <see cref="Backlight.Changed" />
/// </summary>
/// <param name="sender"><see cref="Backlight" /> object responsible for raising the event</param>
/// <param name="e">Event args containing the new state</param>
public delegate void BacklightEventHandler(object sender, BacklightEventArgs e);