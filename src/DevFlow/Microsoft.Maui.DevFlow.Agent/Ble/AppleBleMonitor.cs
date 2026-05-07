#if IOS || MACCATALYST || MACOS
using CoreBluetooth;
using Foundation;
using Microsoft.Maui.DevFlow.Agent.Core;

namespace Microsoft.Maui.DevFlow.Agent.Ble;

internal sealed class AppleBleMonitor : BleMonitor
{
    private CBCentralManager? _centralManager;
    private ICBCentralManagerDelegate? _originalCentralDelegate;
    private CentralManagerDelegateWrapper? _centralWrapper;
    private readonly Dictionary<IntPtr, PeripheralHookInfo> _trackedPeripherals = new();
    private readonly object _hookGate = new();
    private int _hookCount;

    public override bool SupportsScanning => true;
    public override bool IsHooked => _centralManager != null;

    public override void AttachCentralManager(object centralManager)
    {
        if (centralManager is not CBCentralManager cm)
            throw new ArgumentException(
                $"Expected CBCentralManager but got {centralManager.GetType().Name}.",
                nameof(centralManager));

        lock (_hookGate)
        {
            if (ReferenceEquals(_centralManager, cm))
                return; // already hooked to this instance

            if (_centralManager != null)
                DetachCentralManagerLocked();

            _centralManager = cm;
            _originalCentralDelegate = cm.Delegate;
            _centralWrapper = new CentralManagerDelegateWrapper(this, _originalCentralDelegate);
            cm.Delegate = _centralWrapper;
        }

        RecordEvent(new BleEvent { Type = "central_manager_attached" });
    }

    public override void DetachCentralManager()
    {
        lock (_hookGate)
        {
            if (_centralManager == null)
                return;

            DetachCentralManagerLocked();
        }

        RecordEvent(new BleEvent { Type = "central_manager_detached" });
    }

    private void DetachCentralManagerLocked()
    {
        // Restore the original delegate only if ours is still in place
        if (_centralManager != null &&
            _centralWrapper != null &&
            ReferenceEquals(_centralManager.Delegate, _centralWrapper))
        {
            _centralManager.Delegate = _originalCentralDelegate;
        }

        UnhookAllPeripheralsLocked();

        _centralManager = null;
        _originalCentralDelegate = null;
        _centralWrapper = null;
    }

    internal void HookPeripheral(CBPeripheral peripheral)
    {
        lock (_hookGate)
        {
            var handle = peripheral.Handle;
            if (_trackedPeripherals.ContainsKey(handle))
                return;

            var originalDelegate = peripheral.Delegate;
            var wrapper = new PeripheralDelegateWrapper(this, originalDelegate);
            peripheral.Delegate = wrapper;

            _trackedPeripherals[handle] = new PeripheralHookInfo(peripheral, originalDelegate, wrapper);

            _hookCount++;
            if (_hookCount % 20 == 0)
                CleanDeadReferencesLocked();
        }
    }

    internal void UnhookPeripheral(CBPeripheral peripheral)
    {
        lock (_hookGate)
        {
            UnhookPeripheralLocked(peripheral.Handle);
        }
    }

    private void UnhookPeripheralLocked(IntPtr handle)
    {
        if (!_trackedPeripherals.Remove(handle, out var info))
            return;

        if (!info.PeripheralRef.TryGetTarget(out var peripheral))
            return;

        // Only restore if our wrapper is still the delegate
        if (ReferenceEquals(peripheral.Delegate, info.Wrapper))
            peripheral.Delegate = info.OriginalDelegate;
    }

    private void UnhookAllPeripheralsLocked()
    {
        foreach (var handle in _trackedPeripherals.Keys.ToArray())
            UnhookPeripheralLocked(handle);

        _trackedPeripherals.Clear();
    }

    private void CleanDeadReferencesLocked()
    {
        var dead = _trackedPeripherals
            .Where(kv => !kv.Value.PeripheralRef.TryGetTarget(out _))
            .Select(kv => kv.Key)
            .ToArray();

        foreach (var handle in dead)
            _trackedPeripherals.Remove(handle);
    }

    protected override string? StartPlatformScan()
    {
        lock (_hookGate)
        {
            if (_centralManager == null)
                return "No CBCentralManager attached. Call BleMonitor.Instance.AttachCentralManager(centralManager) first.";

            if (_centralManager.State == CBManagerState.PoweredOn)
            {
                _centralManager.ScanForPeripherals(
                    (CBUUID[]?)null,
                    new PeripheralScanningOptions { AllowDuplicatesKey = true });
            }
            // If not powered on yet, the CentralManagerDelegateWrapper.UpdatedState
            // will start the scan when it transitions to PoweredOn.
            return null;
        }
    }

    protected override void StopPlatformScan()
    {
        lock (_hookGate)
        {
            if (_centralManager != null)
            {
                try { _centralManager.StopScan(); }
                catch { /* may already be stopped */ }
            }
        }
    }

    protected override void DisposePlatform()
    {
        lock (_hookGate)
        {
            if (_centralManager != null)
                DetachCentralManagerLocked();
        }
    }

    private sealed class PeripheralHookInfo
    {
        public WeakReference<CBPeripheral> PeripheralRef { get; }
        public ICBPeripheralDelegate? OriginalDelegate { get; }
        public PeripheralDelegateWrapper Wrapper { get; }

        public PeripheralHookInfo(CBPeripheral peripheral, ICBPeripheralDelegate? originalDelegate, PeripheralDelegateWrapper wrapper)
        {
            PeripheralRef = new WeakReference<CBPeripheral>(peripheral);
            OriginalDelegate = originalDelegate;
            Wrapper = wrapper;
        }
    }
}
#endif
