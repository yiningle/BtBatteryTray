namespace BtBatteryTray.Models;

public enum DeviceKind { Other, Keyboard, Mouse, Headphones, Speaker, Gamepad, Pen }

public record BtBattery(string Name, int Level, DeviceKind Kind = DeviceKind.Other);
