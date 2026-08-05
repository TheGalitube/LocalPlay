# LocalPlay für Android

LocalPlay macht ein Android-Telefon oder -Tablet zu einem AirPlay-Empfänger im
lokalen Netzwerk. Die App enthält einen nativen RAOP/RTSP-Kern, veröffentlicht
`_airplay._tcp` und `_raop._tcp` über Android NSD und gibt H.264/PCM über
`MediaCodec` und `AudioTrack` wieder.

## Aktueller Stand

- echter AirPlay-Mirroring-Kern für Legacy-AirPlay ohne PIN
- H.264-Hardwaredecoding und Stereo-PCM-Wiedergabe
- Android-Vordergrunddienst mit Multicast- und WLAN-Lock
- Material-3-Oberfläche mit dynamischen Farben und Edge-to-edge-Layout
- kein leerer Videobereich: Die `SurfaceView` entsteht erst bei eingehenden
  Videodaten und wird dann automatisch im immersiven Vollbild angezeigt
- Android 10+ (`minSdk 29`), `arm64-v8a` und `x86_64`

Der Kern basiert auf
[`dsafa22/AirplayServer`](https://github.com/dsafa22/AirplayServer) in Revision
`5ba1b6965c5b3ab835c8041b10fa0b2fb91a2f6f` und wurde für den aktuellen
Android-NDK sowie Androids öffentliche NSD-API angepasst. Das ist die erste
reale Kompatibilitätsstufe. PIN-Pairing, DRM-Video und eine Zusage für jede
iOS-/macOS-Version sind noch nicht enthalten.

## Build

Voraussetzungen:

- Android Studio mit Android SDK 36
- Android NDK `27.3.13750724`
- CMake `3.22.1`
- JDK 17 oder 21

```bash
cd Android
JAVA_HOME=/path/to/jdk-21 ./gradlew testDebugUnitTest assembleDebug
```

Das Debug-APK liegt danach unter
`app/build/outputs/apk/debug/app-debug.apk`.

## Test auf einem Gerät

```bash
adb install -r app/build/outputs/apk/debug/app-debug.apk
```

Beide Geräte müssen im selben normalen LAN sein. Gast-WLAN, AP-Isolation und
einige VPNs verhindern mDNS-Erkennung oder die direkte Streaming-Verbindung.

Die Architektur, Grenzen und nächsten Abnahmetests stehen in
[`docs/architecture.md`](docs/architecture.md). Hinweise zu den eingebetteten
Komponenten stehen in [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md).
