#if IOS || MACCATALYST || MACOS
using CoreBluetooth;
using Foundation;
using ObjCRuntime;
using Microsoft.Maui.DevFlow.Agent.Core;

namespace Microsoft.Maui.DevFlow.Agent.Ble;

internal sealed class CentralManagerDelegateWrapper : NSObject, ICBCentralManagerDelegate
{
    private readonly AppleBleMonitor _monitor;
    private readonly NSObject? _originalNs;

    public CentralManagerDelegateWrapper(AppleBleMonitor monitor, ICBCentralManagerDelegate? original)
    {
        _monitor = monitor;
        _originalNs = original as NSObject;
    }

    // ── Required ──

    [Export("centralManagerDidUpdateState:")]
    public void UpdatedState(CBCentralManager central)
    {
        _monitor.RecordEvent(new BleEvent
        {
            Type = "adapter_state_changed",
            Data = central.State.ToString()
        });

        if (central.State == CBManagerState.PoweredOn && _monitor.IsScanning)
        {
            central.ScanForPeripherals(
                (CBUUID[]?)null,
                new PeripheralScanningOptions { AllowDuplicatesKey = true });
        }

        ObjCForwarder.Forward(_originalNs, "centralManagerDidUpdateState:", central);
    }

    // ── Optional — record + forward via ObjC runtime ──

    [Export("centralManager:didDiscoverPeripheral:advertisementData:RSSI:")]
    public void DiscoveredPeripheral(CBCentralManager central, CBPeripheral peripheral,
        NSDictionary advertisementData, NSNumber rssi)
    {
        _monitor.RecordScanResult(
            peripheral.Identifier.ToString(),
            peripheral.Name,
            rssi.Int32Value,
            FormatAdvertisementData(advertisementData));

        // Forward first — user may set peripheral.Delegate in their callback
        ObjCForwarder.Forward(_originalNs, "centralManager:didDiscoverPeripheral:advertisementData:RSSI:",
            central, peripheral, advertisementData, rssi);

        // Hook after forwarding so we wrap whatever delegate the user just set
        _monitor.HookPeripheral(peripheral);
    }

    [Export("centralManager:didConnectPeripheral:")]
    public void ConnectedPeripheral(CBCentralManager central, CBPeripheral peripheral)
    {
        _monitor.RecordConnectionStateChanged(
            peripheral.Identifier.ToString(),
            peripheral.Name,
            "connected");

        // Forward first — user may set peripheral.Delegate in their callback
        ObjCForwarder.Forward(_originalNs, "centralManager:didConnectPeripheral:", central, peripheral);

        // Hook after forwarding
        _monitor.HookPeripheral(peripheral);
    }

    [Export("centralManager:didDisconnectPeripheral:error:")]
    public void DisconnectedPeripheral(CBCentralManager central, CBPeripheral peripheral, NSError? error)
    {
        _monitor.RecordConnectionStateChanged(
            peripheral.Identifier.ToString(),
            peripheral.Name,
            "disconnected");

        _monitor.UnhookPeripheral(peripheral);

        ObjCForwarder.Forward(_originalNs, "centralManager:didDisconnectPeripheral:error:", central, peripheral, error);
    }

    [Export("centralManager:didFailToConnectPeripheral:error:")]
    public void FailedToConnectPeripheral(CBCentralManager central, CBPeripheral peripheral, NSError? error)
    {
        _monitor.RecordEvent(new BleEvent
        {
            Type = "connection_failed",
            DeviceId = peripheral.Identifier.ToString(),
            DeviceName = peripheral.Name,
            Data = error?.LocalizedDescription
        });

        _monitor.UnhookPeripheral(peripheral);

        ObjCForwarder.Forward(_originalNs, "centralManager:didFailToConnectPeripheral:error:", central, peripheral, error);
    }

    [Export("centralManager:willRestoreState:")]
    public void WillRestoreState(CBCentralManager central, NSDictionary dict)
    {
        ObjCForwarder.Forward(_originalNs, "centralManager:willRestoreState:", central, dict);
    }

    [Export("centralManager:connectionEventDidOccur:forPeripheral:")]
    public void ConnectionEventDidOccur(CBCentralManager central, CBConnectionEvent connectionEvent, CBPeripheral peripheral)
    {
        _monitor.RecordEvent(new BleEvent
        {
            Type = "connection_event",
            DeviceId = peripheral.Identifier.ToString(),
            DeviceName = peripheral.Name,
            Data = connectionEvent.ToString()
        });

        ObjCForwarder.Forward(_originalNs, "centralManager:connectionEventDidOccur:forPeripheral:",
            central, (nint)(long)connectionEvent, peripheral);
    }

    [Export("centralManager:didUpdateANCSAuthorizationForPeripheral:")]
    public void DidUpdateAncsAuthorization(CBCentralManager central, CBPeripheral peripheral)
    {
        _monitor.RecordEvent(new BleEvent
        {
            Type = "ancs_authorization_updated",
            DeviceId = peripheral.Identifier.ToString(),
            DeviceName = peripheral.Name
        });

        ObjCForwarder.Forward(_originalNs, "centralManager:didUpdateANCSAuthorizationForPeripheral:", central, peripheral);
    }

    private static string? FormatAdvertisementData(NSDictionary? data)
    {
        if (data == null || data.Count == 0) return null;
        var parts = new List<string>();
        foreach (var key in data.Keys)
        {
            var value = data[key];
            if (value is NSData nsData)
                parts.Add($"{key}={Convert.ToHexString(nsData.ToArray())}");
            else
                parts.Add($"{key}={value}");
        }
        return string.Join(";", parts);
    }
}
#endif
