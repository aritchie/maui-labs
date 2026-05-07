#if IOS || MACCATALYST || MACOS
using CoreBluetooth;
using Foundation;
using ObjCRuntime;
using Microsoft.Maui.DevFlow.Agent.Core;

namespace Microsoft.Maui.DevFlow.Agent.Ble;

internal sealed class PeripheralDelegateWrapper : NSObject, ICBPeripheralDelegate
{
    private readonly AppleBleMonitor _monitor;
    private readonly NSObject? _originalNs;

    public PeripheralDelegateWrapper(AppleBleMonitor monitor, ICBPeripheralDelegate? original)
    {
        _monitor = monitor;
        _originalNs = original as NSObject;
    }

    private static (string id, string? name) DeviceInfo(CBPeripheral p)
        => (p.Identifier.ToString(), p.Name);

    [Export("peripheral:didDiscoverServices:")]
    public void DiscoveredService(CBPeripheral peripheral, NSError? error)
    {
        var (id, name) = DeviceInfo(peripheral);
        if (error == null && peripheral.Services != null)
        {
            foreach (var svc in peripheral.Services)
                _monitor.RecordServiceDiscovered(id, name, svc.UUID.ToString());
        }

        ObjCForwarder.Forward(_originalNs, "peripheral:didDiscoverServices:", peripheral, error);
    }

    [Export("peripheral:didDiscoverIncludedServicesForService:error:")]
    public void DiscoveredIncludedService(CBPeripheral peripheral, CBService service, NSError? error)
    {
        ObjCForwarder.Forward(_originalNs, "peripheral:didDiscoverIncludedServicesForService:error:", peripheral, service, error);
    }

    [Export("peripheral:didDiscoverCharacteristicsForService:error:")]
    public void DiscoveredCharacteristic(CBPeripheral peripheral, CBService service, NSError? error)
    {
        var (id, name) = DeviceInfo(peripheral);
        if (error == null && service.Characteristics != null)
        {
            foreach (var ch in service.Characteristics)
            {
                _monitor.RecordEvent(new BleEvent
                {
                    Type = "characteristic_discovered",
                    DeviceId = id,
                    DeviceName = name,
                    ServiceUuid = service.UUID.ToString(),
                    CharacteristicUuid = ch.UUID.ToString()
                });
            }
        }

        ObjCForwarder.Forward(_originalNs, "peripheral:didDiscoverCharacteristicsForService:error:", peripheral, service, error);
    }

    [Export("peripheral:didUpdateValueForCharacteristic:error:")]
    public void UpdatedCharacterteristicValue(CBPeripheral peripheral, CBCharacteristic characteristic, NSError? error)
    {
        if (error == null)
        {
            var (id, name) = DeviceInfo(peripheral);
            var serviceUuid = characteristic.Service?.UUID?.ToString() ?? "";
            var charUuid = characteristic.UUID.ToString();
            var valueHex = characteristic.Value != null
                ? Convert.ToHexString(characteristic.Value.ToArray())
                : null;

            if (characteristic.IsNotifying)
                _monitor.RecordNotification(id, name, serviceUuid, charUuid, valueHex);
            else
                _monitor.RecordCharacteristicRead(id, name, serviceUuid, charUuid, valueHex);
        }

        ObjCForwarder.Forward(_originalNs, "peripheral:didUpdateValueForCharacteristic:error:", peripheral, characteristic, error);
    }

    [Export("peripheral:didWriteValueForCharacteristic:error:")]
    public void WroteCharacteristicValue(CBPeripheral peripheral, CBCharacteristic characteristic, NSError? error)
    {
        if (error == null)
        {
            var (id, name) = DeviceInfo(peripheral);
            _monitor.RecordCharacteristicWrite(
                id, name,
                characteristic.Service?.UUID?.ToString() ?? "",
                characteristic.UUID.ToString(),
                null,
                withResponse: true);
        }

        ObjCForwarder.Forward(_originalNs, "peripheral:didWriteValueForCharacteristic:error:", peripheral, characteristic, error);
    }

    [Export("peripheral:didUpdateNotificationStateForCharacteristic:error:")]
    public void UpdatedNotificationState(CBPeripheral peripheral, CBCharacteristic characteristic, NSError? error)
    {
        if (error == null)
        {
            var (id, name) = DeviceInfo(peripheral);
            _monitor.RecordEvent(new BleEvent
            {
                Type = "notification_state_changed",
                DeviceId = id,
                DeviceName = name,
                ServiceUuid = characteristic.Service?.UUID?.ToString(),
                CharacteristicUuid = characteristic.UUID.ToString(),
                Data = characteristic.IsNotifying ? "enabled" : "disabled"
            });
        }

        ObjCForwarder.Forward(_originalNs, "peripheral:didUpdateNotificationStateForCharacteristic:error:", peripheral, characteristic, error);
    }

    [Export("peripheralIsReadyToSendWriteWithoutResponse:")]
    public void IsReadyToSendWriteWithoutResponse(CBPeripheral peripheral)
    {
        ObjCForwarder.Forward(_originalNs, "peripheralIsReadyToSendWriteWithoutResponse:", peripheral);
    }

    [Export("peripheral:didDiscoverDescriptorsForCharacteristic:error:")]
    public void DiscoveredDescriptor(CBPeripheral peripheral, CBCharacteristic characteristic, NSError? error)
    {
        ObjCForwarder.Forward(_originalNs, "peripheral:didDiscoverDescriptorsForCharacteristic:error:", peripheral, characteristic, error);
    }

    [Export("peripheral:didUpdateValueForDescriptor:error:")]
    public void UpdatedValue(CBPeripheral peripheral, CBDescriptor descriptor, NSError? error)
    {
        if (error == null)
        {
            var (id, name) = DeviceInfo(peripheral);
            _monitor.RecordEvent(new BleEvent
            {
                Type = "descriptor_read",
                DeviceId = id,
                DeviceName = name,
                ServiceUuid = descriptor.Characteristic?.Service?.UUID?.ToString(),
                CharacteristicUuid = descriptor.Characteristic?.UUID?.ToString(),
                DescriptorUuid = descriptor.UUID?.ToString(),
                Data = descriptor.Value?.ToString()
            });
        }

        ObjCForwarder.Forward(_originalNs, "peripheral:didUpdateValueForDescriptor:error:", peripheral, descriptor, error);
    }

    [Export("peripheral:didWriteValueForDescriptor:error:")]
    public void WroteDescriptorValue(CBPeripheral peripheral, CBDescriptor descriptor, NSError? error)
    {
        if (error == null)
        {
            var (id, name) = DeviceInfo(peripheral);
            _monitor.RecordDescriptorWrite(
                id, name,
                descriptor.Characteristic?.Service?.UUID?.ToString() ?? "",
                descriptor.Characteristic?.UUID?.ToString() ?? "",
                descriptor.UUID?.ToString() ?? "",
                null);
        }

        ObjCForwarder.Forward(_originalNs, "peripheral:didWriteValueForDescriptor:error:", peripheral, descriptor, error);
    }

    [Export("peripheralDidUpdateName:")]
    public void UpdatedName(CBPeripheral peripheral)
    {
        ObjCForwarder.Forward(_originalNs, "peripheralDidUpdateName:", peripheral);
    }

    [Export("peripheral:didReadRSSI:error:")]
    public void RssiRead(CBPeripheral peripheral, NSNumber rssi, NSError? error)
    {
        if (error == null)
        {
            var (id, name) = DeviceInfo(peripheral);
            _monitor.RecordEvent(new BleEvent
            {
                Type = "rssi_read",
                DeviceId = id,
                DeviceName = name,
                Rssi = rssi.Int32Value
            });
        }

        ObjCForwarder.Forward(_originalNs, "peripheral:didReadRSSI:error:", peripheral, rssi, error);
    }

    [Export("peripheral:didModifyServices:")]
    public void ModifiedServices(CBPeripheral peripheral, CBService[] services)
    {
        // CBService[] maps to NSArray in ObjC — use NSArray for forwarding
        var nsArray = NSArray.FromNSObjects(services);
        ObjCForwarder.Forward(_originalNs, "peripheral:didModifyServices:", peripheral, nsArray);
    }

    [Export("peripheral:didOpenL2CAPChannel:error:")]
    public void DidOpenL2CapChannel(CBPeripheral peripheral, CBL2CapChannel? channel, NSError? error)
    {
        ObjCForwarder.Forward(_originalNs, "peripheral:didOpenL2CAPChannel:error:", peripheral, channel, error);
    }
}
#endif
