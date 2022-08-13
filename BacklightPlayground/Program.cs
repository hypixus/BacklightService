// See https://aka.ms/new-console-template for more information

using BacklightLibrary;

Console.WriteLine("Hello, World!");
var backlightRich = new BacklightKeeper(BacklightState.Full);
backlightRich.OnException += (_, eventArgs) => Console.WriteLine(eventArgs.Exception);
backlightRich.Start();
Thread.Sleep(10000);
backlightRich.Stop();
Console.WriteLine("Successfully ended the session.");