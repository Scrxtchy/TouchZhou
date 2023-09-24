using Device.Net;
using Hid.Net.Windows;
using System.Runtime.InteropServices;
using WMPointer;
using System.Diagnostics;
using WMPointer.TouchInjection;

[DllImport("User32")]
static extern bool RegisterPointerInputTarget(IntPtr hwnd, INPUT_TYPE pointerType);

int contactpoints = 16;

RegisterPointerInputTarget(Process.GetCurrentProcess().MainWindowHandle, INPUT_TYPE.TOUCH);

var hidFactory = new FilterDeviceDefinition(vendorId: 0x1973, productId: 0x2001, label: "大四 TouchPad").CreateWindowsHidDeviceFactory();

var devdef = (await hidFactory.GetConnectedDeviceDefinitionsAsync().ConfigureAwait(false)).ToList();
if (devdef.Count == 0) return;
var zhou = await hidFactory.GetDeviceAsync(devdef.First()).ConfigureAwait(false);
await zhou.InitializeAsync().ConfigureAwait(false);
var buffer = new byte[64];
var ground = new byte[32];
var last = new byte[16];
var pointerid = Enumerable.Repeat((uint)new Random().Next(-int.MaxValue, int.MaxValue), contactpoints).ToArray();

if (!WMPointer.TouchInjection.Win32.InitializeTouchInjection(contactpoints, TOUCH_FEEDBACK.DEFAULT))
{
    Console.WriteLine($"Failed to Inject Touch {Marshal.GetLastWin32Error()}");
    return;
}

while (true)
{
    var readBuffer = await zhou.WriteAndReadAsync(buffer).ConfigureAwait(false);
    Array.Copy(readBuffer, 3, ground, 0, 32);
    var touch = new byte[16];

    var contacts = new POINTER_TOUCH_INFO[contactpoints];

    for (int i = 0; i < ground.Length; i++)
    {
        int pos = i / 2;
        if (touch[pos] < ground[i]) touch[pos] = ground[i];

    }
    List<POINTER_TOUCH_INFO> sends = new List<POINTER_TOUCH_INFO>();

    for (int i = 0; i < contactpoints; i++)
    {
        if (touch[i] == 0 && last[i] == 0)
        {
            pointerid[i] = (uint)new Random().Next(-int.MaxValue, int.MaxValue);
            continue;
        }

        contacts[i].pointerInfo.pointerType = INPUT_TYPE.TOUCH;
        contacts[i].pointerInfo.pointerId = (uint)i;
        contacts[i].pointerInfo.ptPixelLocationY = 300 + (touch[i] * 2);
        contacts[i].pointerInfo.ptPixelLocationX = 10 + ((1800 / 16) * i);
        contacts[i].touchFlags = TOUCH_FLAGS.TOUCH_FLAG_NONE;
        contacts[i].touchMask = TOUCH_MASK.CONTACTAREA | TOUCH_MASK.ORIENTATION | TOUCH_MASK.PRESSURE;
        contacts[i].rcContactTop = contacts[i].pointerInfo.ptPixelLocationY - 2;
        contacts[i].rcContactBottom = contacts[i].pointerInfo.ptPixelLocationY + 2;
        contacts[i].rcContactLeft = contacts[i].pointerInfo.ptPixelLocationX - 2;
        contacts[i].rcContactRight = contacts[i].pointerInfo.ptPixelLocationX + 2;

        if (touch[i] == 0)
        {
            contacts[i].pointerInfo.pointerFlags = MESSAGE_FLAGS.UP | MESSAGE_FLAGS.UPDATE;
        }
        else
        {
            if (touch[i] < 60 && touch[i] != 0)
            {

                contacts[i].pointerInfo.pointerFlags = MESSAGE_FLAGS.INRANGE;
                if (last[i] < 60) contacts[i].pointerInfo.pointerFlags = contacts[i].pointerInfo.pointerFlags | MESSAGE_FLAGS.UPDATE;
                if (last[i] >= 60) contacts[i].pointerInfo.pointerFlags = contacts[i].pointerInfo.pointerFlags | MESSAGE_FLAGS.UP;
            }
            if (touch[i] >= 60)
            {
                contacts[i].pointerInfo.pointerFlags = MESSAGE_FLAGS.INCONTACT | MESSAGE_FLAGS.INRANGE;
                if (last[i] < 60) contacts[i].pointerInfo.pointerFlags = contacts[i].pointerInfo.pointerFlags | MESSAGE_FLAGS.DOWN;
                else contacts[i].pointerInfo.pointerFlags = contacts[i].pointerInfo.pointerFlags | MESSAGE_FLAGS.UPDATE;
            }
        }

        sends.Add(contacts[i]);
    }

    if (sends.Count != 0)
    {
        WMPointer.TouchInjection.Win32.InjectTouchInput(sends.Count, sends.ToArray());
        foreach (var item in sends)
        {
            Console.Write($"#{item.pointerInfo.pointerId} [{touch[item.pointerInfo.pointerId]}] - {item.pointerInfo.pointerFlags}");
            Console.WriteLine();
        }

    }
    Array.Copy(touch, last, touch.Length);
}

