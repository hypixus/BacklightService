namespace BacklightLibrary;

public static class EventHandlerExtensions
{
    // Null checks before trying to actually invoke the methods.
    public static void SafeInvoke(this ExceptionEventHandler? handler,
        object sender, ExceptionEventArgs args)
    {
        handler?.Invoke(sender, args);
    }

    // Null checks before trying to actually invoke the methods.
    public static void SafeInvoke(this BacklightEventHandler? handler,
        object sender, BacklightEventArgs args)
    {
        handler?.Invoke(sender, args);
    }
}