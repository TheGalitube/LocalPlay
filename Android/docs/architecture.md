# Android-Empfänger: Architektur

## Datenweg

```text
Material-3 Activity
  ├─ Dashboard (nicht verbunden)
  └─ SurfaceView (nur verbunden, immersives Vollbild)
       │
Foreground ReceiverService
  ├─ Notification und Stop-Aktion
  ├─ MulticastLock / WifiLock
  ├─ Android NSD: _airplay._tcp + _raop._tcp
  └─ JNI-Steuerung
       │
Native AirPlayServer-Kern
  ├─ RAOP / RTSP / FairPlay-Handshake
  ├─ AirPlay-Mirroring und H.264 Annex B
  └─ AAC/ALAC-Decoding zu Stereo-PCM
       │
AndroidMediaReceiver
  ├─ MediaCodec (H.264 → Surface)
  └─ AudioTrack (44,1 kHz, PCM 16 Bit, Stereo)
```

## Lebenszyklus

1. Der Nutzer startet den Empfänger bewusst im Dashboard.
2. Der Vordergrunddienst öffnet den nativen Server und veröffentlicht beide
   DNS-SD-Dienste über `NsdManager`.
3. Erst mit dem ersten eingehenden Videoframe wechselt der Zustand auf
   `CONNECTED`. Compose ersetzt das Dashboard dann durch eine schwarze
   `SurfaceView` und blendet die Systemleisten aus.
4. Bei Verbindungsende wird der Decoder zurückgesetzt und das Dashboard wieder
   sichtbar. Beim Stoppen werden DNS-SD, native Threads, Decoder und Locks
   freigegeben.

## Bereits umgesetzt

- Android 10+ (`minSdk 29`)
- ARM64-Geräte und x86_64-Emulatoren
- stabiler lokal administrierter Geräte-Identifier pro Android-Installation
- Android-NSD statt Zugriff auf private mDNS-System-Sockets
- begrenzte Video-/Audioqueues gegen unbegrenzten Latenzaufbau
- Surface-Neuerstellung bei Rotation und App-Lebenszyklus
- Material 3, dynamische Farben, Edge-to-edge und immersives Playback

## Aktuelle Grenzen

- erste Kompatibilitätsstufe: Legacy-AirPlay-Mirroring ohne PIN
- H.264; HEVC wird nicht beworben
- keine DRM-geschützten Streams und kein AirPlay-2-Multiroom
- Netzwerkwechsel während einer Sitzung wird noch nicht automatisch neu
  gebunden
- Audio-/Video-Synchronität und Langzeitlatenz brauchen noch Messungen mit
  mehreren iOS-, iPadOS- und macOS-Versionen

## Abnahmetests vor einem Release

1. Sichtbarkeit binnen fünf Sekunden auf iPhone, iPad und Mac.
2. Verbindung, Trennung und Wiederverbindung je zehnmal ohne Absturz.
3. H.264-Mirroring mit Ton mindestens 30 Minuten.
4. Rotation und Display aus/an während einer Verbindung.
5. Stoppen entfernt beide DNS-SD-Dienste und gibt den Port frei.
6. Tests in 2,4-, 5- und 6-GHz-WLAN sowie mit problematischem Gast-WLAN.

## Lizenzgrenze

Der native Kern enthält LGPL-, GPL-, MIT-, BSD- und FDK-AAC-Komponenten. Die
Quellen und vorhandenen Lizenztexte liegen unter
`app/src/main/cpp/airplayserver/`; eine öffentliche Distribution muss die
jeweiligen Bedingungen erfüllen. Details stehen in
`Android/THIRD_PARTY_NOTICES.md`.
