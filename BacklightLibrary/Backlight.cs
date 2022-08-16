/*
 * Copyright 2019 Parth Patel
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      https://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable UnusedType.Global

using System.Runtime.InteropServices;
using BacklightLibrary.Events;
using Microsoft.Win32;

namespace BacklightLibrary;

/// <summary>
///     Friendly read/write access to keyboard backlight
/// </summary>
public class Backlight
{
    private const string PmSubKey = @"SYSTEM\CurrentControlSet\Services\IBMPMSVC\Parameters\Notification";
    private const string BacklightMutexName = "BacklightLibraryInternalMutex";
    private readonly Mutex _backlightMutex;
    private bool _enabled;
    private Thread _loopThread;

    /// <summary>
    /// Create a new instance, automatically queries for <see cref="State" /> and <see cref="Limit" />
    /// </summary>
    /// <exception cref="Exception">Cannot create multiple instances/driver access error.</exception>
    public Backlight()
    {
        _backlightMutex = new Mutex(false, BacklightMutexName, out var isNewMutex);
        if (!isNewMutex) throw new Exception("Cannot initialize multiple instances of Backlight class.");

        if (!GetKeyboardBackLightStatus(out var status))
            throw new Exception("Error accessing Keyboard driver");
        if (!GetKeyboardBackLightLevel(out var limit))
            throw new Exception("Error accessing Keyboard driver");
        State = status;
        Limit = limit;

        _loopThread = new Thread(MonitorThread);
    }

    /// <summary>
    ///     Get/set the enabled state, same as Start()/Stop()
    /// </summary>
    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value) return;
            _enabled = value;
            if (value)
            {
                _loopThread = new Thread(MonitorThread);
                _loopThread.Start();
            }
            else
            {
                _loopThread.Join();
            }
        }
    }

    /// <summary>
    ///     Get the last known state (ex. 0/1/2) of the backlight recorded by last <see cref="ReadState" />
    ///     <see cref="ChangeState" />
    /// </summary>
    public int State { get; private set; }

    /// <summary>
    ///     Maximum brightness level supported by this keyboard (ex. 2 --> 0/1/2 supported states)
    /// </summary>
    public int Limit { get; }

    /// <summary>
    ///     Raised when the backlight state has been changed by keyboard or by <see cref="ChangeState" />
    /// </summary>
    public event BacklightEventHandler? OnChanged;

    /// <summary>
    ///     Raised when an internal exception has occurred.
    /// </summary>
    public event ExceptionEventHandler? OnException;

    /// <summary>
    ///     Begin background processing
    /// </summary>
    public void Start()
    {
        Enabled = true;
    }

    /// <summary>
    ///     End background processing
    /// </summary>
    public void Stop()
    {
        Enabled = false;
    }

    /// <summary>
    ///     Update backlight with a new state
    /// </summary>
    /// <param name="state">The state to be written (ex. 0/1/2)</param>
    public void ChangeState(int state)
    {
        State = Clamp(state);
        if (!SetKeyboardBackLightStatus(state))
            OnException.SafeInvoke(this, new ExceptionEventArgs(new Exception("Error accessing the keyboard driver")));
    }

    /// <summary>
    ///     Query backlight for current state
    /// </summary>
    /// <returns>The state read from keyboard, range 0 to Limit</returns>
    public int ReadState()
    {
        if (!GetKeyboardBackLightStatus(out var st))
            throw new Exception("Error when accessing the current keyboard state.");
        State = Clamp(st);
        return State;
    }

    private void MonitorThread()
    {
        using var ev = new EventWaitHandle(false, EventResetMode.AutoReset);
        using var notifyKey = Registry.LocalMachine.OpenSubKey(PmSubKey);
        if (notifyKey == null) return;

        var registryKeyValue = GetRegistryKeyValue(notifyKey);
        var resultCode = RegNotifyChangeKeyValue(notifyKey.Handle.DangerousGetHandle(), false, 4,
            ev.SafeWaitHandle.DangerousGetHandle(), true);
        if (resultCode != 0)
            InvokeOnException(new Exception("RegNotifyChangeKeyValue failed with code " + resultCode));
        while (_enabled)
        {
            // ensures the loop does not choke the system.
            Thread.Sleep(250);
            // Capture value, re-register for notification event
            var oldValue = registryKeyValue;
            registryKeyValue = GetRegistryKeyValue(notifyKey);
            resultCode = RegNotifyChangeKeyValue(notifyKey.Handle.DangerousGetHandle(), false, 4,
                ev.SafeWaitHandle.DangerousGetHandle(), true);
            if (resultCode != 0)
            {
                InvokeOnException(new Exception("RegNotifyChangeKeyValue failed with code " + resultCode));
                continue;
            }

            // Check notification reason, bit.17 should flip; invoke event on parent thread
            if ((oldValue ^ registryKeyValue) >> 17 != 1) continue;
            try
            {
                var currentState = ReadState();
                InvokeOnChanged(currentState);
            }
            catch (Exception ex)
            {
                InvokeOnException(ex);
            }
        }
    }

    private static uint GetRegistryKeyValue(RegistryKey notifyKey)
    {
        return (uint)(int)(notifyKey.GetValue(null) ?? throw new NullReferenceException());
    }

    private int Clamp(int st)
    {
        if (st < 0) st = 0;
        if (st > Limit) st = Limit;
        return st;
    }

    #region invokeEvents

    private void InvokeOnException(Exception ex)
    {
        var invokeThread = new Thread(() => OnException.SafeInvoke(this, new ExceptionEventArgs(ex)));
        invokeThread.Start();
    }

    private void InvokeOnChanged(int state)
    {
        var invokeThread = new Thread(() => OnChanged.SafeInvoke(this, new BacklightEventArgs(state)));
        invokeThread.Start();
    }

    #endregion

    #region backlightCalls

    private bool GetKeyboardBackLightStatus(out int status)
    {
        _backlightMutex.WaitOne();
        try
        {
            var code = CallPmService(2238080, 0);
            if ((code & 0x0050000) != 0x0050000) throw new Exception("Backlight hw not ready");
            status = code & 0xF;
            _backlightMutex.ReleaseMutex();
            return true;
        }
        catch (Exception)
        {
            status = 0;
            _backlightMutex.ReleaseMutex();
            return false;
        }
    }

    private bool GetKeyboardBackLightLevel(out int level)
    {
        _backlightMutex.WaitOne();
        try
        {
            var code = CallPmService(2238080, 0);
            if ((code & 0x0050000) != 0x0050000) throw new Exception("Backlight hw not ready");
            level = (code >> 8) & 0xF;
            _backlightMutex.ReleaseMutex();
            return true;
        }
        catch (Exception)
        {
            level = 0;
            _backlightMutex.ReleaseMutex();
            return false;
        }
    }

    private bool SetKeyboardBackLightStatus(int status)
    {
        _backlightMutex.WaitOne();
        try
        {
            var code = CallPmService(2238080, 0);
            if ((code & 0x0050000) != 0x0050000) throw new Exception("Backlight hw not ready");
            var arg = ((code & 0x00200000) != 0 ? 0x100 : 0) | (code & 0xF0) | status;
            var ret = CallPmService(2238084, arg);
            _backlightMutex.ReleaseMutex();
            return ret == arg;
        }
        catch (Exception)
        {
            _backlightMutex.ReleaseMutex();
            return false;
        }
    }

    private static int CallPmService(uint code, int input)
    {
        var handlePtr = CreateFile(@"\\.\IBMPmDrv", 0x80000000, 1, IntPtr.Zero, 3, 0, IntPtr.Zero);
        if (handlePtr == IntPtr.Zero)
            throw new Exception("Error opening handle to PM service");

        // ReSharper disable once NotAccessedVariable
        uint bytesReturned;
        NativeOverlapped overlapped = default;
        var inp = BitConverter.GetBytes(input);
        var lpOutBuffer = new byte[sizeof(int)];
        var controlHandle = DeviceIoControl(handlePtr, code, inp, (uint)inp.Length, lpOutBuffer,
            (uint)lpOutBuffer.Length, out bytesReturned, ref overlapped);
        var output = BitConverter.ToInt32(lpOutBuffer, 0);

        if (!controlHandle)
            throw new Exception("Error passing control info to PM service");
        controlHandle = CloseHandle(handlePtr);
        if (!controlHandle)
            throw new Exception("Error closing handle to PM service");
        return output;
    }

    #endregion

    #region externs

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode,
        IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(IntPtr hDevice, uint dwIoControlCode,
        [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] [In]
        byte[] lpInBuffer, uint nInBufferSize,
        [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 6)] [Out]
        byte[] lpOutBuffer, uint nOutBufferSize,
        out uint lpBytesReturned, ref NativeOverlapped lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern int RegNotifyChangeKeyValue(IntPtr hKey, bool watchSubtree, uint notifyFilter,
        IntPtr hEvent, bool asynchronous);

    #endregion
}