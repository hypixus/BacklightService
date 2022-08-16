# BacklightService

## What is this?

A very simple windows service utilizing work of [pspatel321](https://github.com/pspatel321/auto-backlight-for-thinkpad), that basically forces the keyboard backlight to remain turned on all the time. Contrary to other solutions, an attempt to change backlight by hand using Fn+Space or in UI should not cause system to bluescreen. Tested only on my second-hand X390 Yoga, so please use at your own risk. If you notice anything alarming in the code, or just have a good idea to implement, please submit an issue - all constructive feedback is welcome.

## What is under the hood?

Entirety of the solution relies on 3 projects.
- BacklightLibrary
  This is where actual wrapping of system calls happens. Everything relies on BacklightKeeper class, which after instantiation should be capable of safely accessing the system resources. Changes made to original [Backlight.cs](https://github.com/pspatel321/auto-backlight-for-thinkpad/blob/master/Auto%20Backlight%20for%20ThinkPad/Backlight.cs) include - but are not limited to - better thread safety, refactorization, and change of naming conventions.
- BacklightService
  An extremely simple windows service that instantiates BacklightKeeper class and reports any errors occurring. One parameter that can be given before runtime is the backlight level (0-2), by default being 2.
- BacklightPlayground
  Basic console application to test functionality of the library. As the name suggests, its just a sandbox to test library out - I highly recommend doing so before actually installing the service.

## Installation
  For now it is manual by typing one command in the terminal, future installation scripts are in the plan.
  To install, simply run cmd.exe as admin and type the following, replacing <fullPathToFile> with location of the executable.
  ```
  sc create "BacklightService" binPath="<fullPathToFile>\BacklightService.exe" start=delayed-auto displayname="Backlight Service"
  ```
  To start the service before machine reboots, type
  ```
  sc start BacklightService
  ```
  That's it! Now your backlight turns itself on automatically :)
## Removing
  If you decide to remove the service, please do the following.
  First, make sure the service is stopped by
  ```
  sc stop BacklightService
  ```
  Once the service stopped, remove its entry in the system.
  ```
  sc delete BacklightService
  ```
  You may encounter an error whilst trying to remove it, just give Windows a few seconds to stop the process and delete it. That't all folks!
