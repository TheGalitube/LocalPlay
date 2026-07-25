# LocalPlay

LocalPlay turns a Windows 10/11 PC into a local AirPlay display for iPhone,
iPad, and Mac. It provides a native Windows launcher around the open-source
[UxPlay](https://github.com/FDH2/UxPlay) receiver.

## What works

- Mirror an iPhone, iPad, or Mac display with audio.
- Offer the receiver to macOS as a second AirPlay display.
- Pair new devices with an Apple-style one-time PIN.
- Keep inbound access on the Windows **Private** network profile and the local
  subnet through a dedicated firewall rule.
- Request 1080p, 1440p/2K, or 4K at up to 60 FPS from compatible clients.
  Modes above 1080p automatically enable HEVC.

AirPlay is proprietary. LocalPlay relies on UxPlay's reverse-engineered legacy
AirPlay 2 protocol. DRM-protected video, AirPlay 2 multi-room audio, keyboard
and mouse forwarding, and guaranteed compatibility with future Apple releases
are outside the MVP.

## Build and run

From PowerShell:

```powershell
.\scripts\bootstrap.ps1
.\scripts\run.ps1
```

The bootstrap installs build dependencies into the current user/workspace,
builds UxPlay, and publishes the WPF app. It does not install a background
service.

The built app is placed in `artifacts\LocalPlay`. When first starting the
receiver, use **Firewall freigeben** in the app and accept Windows' UAC prompt.
The resulting rule is limited to private networks and `LocalSubnet`.

## Use from Apple devices

- iPhone/iPad: open Control Center, tap **Screen Mirroring**, and choose
  **LocalPlay**.
- Mac: open Control Center, choose **Screen Mirroring**, then **LocalPlay**.
  macOS can offer **Use As Separate Display** for a second desktop.
- Enter the four-digit PIN shown by LocalPlay when pairing a device for the
  first time.

Both devices must be on the same LAN. Guest Wi-Fi often blocks device-to-device
traffic and mDNS discovery.

## Development

The UI targets .NET 8 WPF. The receiver is built from a pinned UxPlay commit by
`scripts\bootstrap.ps1`; generated tools, sources, and binaries remain ignored.

```powershell
.\scripts\test.ps1
```

## Licensing

LocalPlay application code is MIT licensed. UxPlay and the receiver code it
contains are GPLv3; see [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
Distributing a package that contains UxPlay requires GPLv3 compliance and the
corresponding source/license notices.
