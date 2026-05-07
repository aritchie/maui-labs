#if IOS || MACCATALYST || MACOS
using System.Runtime.InteropServices;
using Foundation;
using ObjCRuntime;

namespace Microsoft.Maui.DevFlow.Agent.Ble;

/// <summary>
/// Forwards ObjC delegate callbacks to the original delegate via objc_msgSend.
/// Used because optional protocol methods are not exposed on C# interfaces
/// (ICBCentralManagerDelegate, ICBPeripheralDelegate) — only on the abstract base classes.
/// </summary>
internal static class ObjCForwarder
{
    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern void void_objc_msgSend_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg1);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern void void_objc_msgSend_IntPtr_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr arg2);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern void void_objc_msgSend_IntPtr_IntPtr_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr arg2, IntPtr arg3);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern void void_objc_msgSend_IntPtr_IntPtr_IntPtr_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr arg2, IntPtr arg3, IntPtr arg4);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern void void_objc_msgSend_IntPtr_nint_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg1, nint arg2, IntPtr arg3);

    /// <summary>
    /// Forwards a 1-arg ObjC message if the target responds to the selector.
    /// </summary>
    public static void Forward(NSObject? target, string selectorName, NSObject arg1)
    {
        if (target == null) return;
        var sel = Selector.GetHandle(selectorName);
        if (!target.RespondsToSelector(new Selector(selectorName))) return;
        void_objc_msgSend_IntPtr(target.Handle, sel, arg1.Handle);
    }

    /// <summary>
    /// Forwards a 2-arg ObjC message if the target responds to the selector.
    /// </summary>
    public static void Forward(NSObject? target, string selectorName, NSObject arg1, NSObject? arg2)
    {
        if (target == null) return;
        var sel = Selector.GetHandle(selectorName);
        if (!target.RespondsToSelector(new Selector(selectorName))) return;
        void_objc_msgSend_IntPtr_IntPtr(target.Handle, sel, arg1.Handle, arg2?.Handle ?? IntPtr.Zero);
    }

    /// <summary>
    /// Forwards a 3-arg ObjC message if the target responds to the selector.
    /// </summary>
    public static void Forward(NSObject? target, string selectorName, NSObject arg1, NSObject arg2, NSObject? arg3)
    {
        if (target == null) return;
        var sel = Selector.GetHandle(selectorName);
        if (!target.RespondsToSelector(new Selector(selectorName))) return;
        void_objc_msgSend_IntPtr_IntPtr_IntPtr(target.Handle, sel, arg1.Handle, arg2.Handle, arg3?.Handle ?? IntPtr.Zero);
    }

    /// <summary>
    /// Forwards a 4-arg ObjC message if the target responds to the selector.
    /// </summary>
    public static void Forward(NSObject? target, string selectorName, NSObject arg1, NSObject arg2, NSObject? arg3, NSObject arg4)
    {
        if (target == null) return;
        var sel = Selector.GetHandle(selectorName);
        if (!target.RespondsToSelector(new Selector(selectorName))) return;
        void_objc_msgSend_IntPtr_IntPtr_IntPtr_IntPtr(target.Handle, sel, arg1.Handle, arg2.Handle, arg3?.Handle ?? IntPtr.Zero, arg4.Handle);
    }

    /// <summary>
    /// Forwards a 3-arg ObjC message with an nint middle arg if the target responds to the selector.
    /// Used for connectionEventDidOccur where the event is an enum (nint).
    /// </summary>
    public static void Forward(NSObject? target, string selectorName, NSObject arg1, nint arg2, NSObject arg3)
    {
        if (target == null) return;
        var sel = Selector.GetHandle(selectorName);
        if (!target.RespondsToSelector(new Selector(selectorName))) return;
        void_objc_msgSend_IntPtr_nint_IntPtr(target.Handle, sel, arg1.Handle, arg2, arg3.Handle);
    }
}
#endif
