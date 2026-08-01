# LocalPlay

[![Build](https://github.com/TheGalitube/LocalPlay/actions/workflows/ci.yml/badge.svg)](https://github.com/TheGalitube/LocalPlay/actions/workflows/ci.yml)
[![Latest release](https://img.shields.io/github/v/release/TheGalitube/LocalPlay?display_name=tag)](https://github.com/TheGalitube/LocalPlay/releases/latest)
[![License](https://img.shields.io/github/license/TheGalitube/LocalPlay)](LICENSE)

LocalPlay macht einen Windows-10/11-PC zu einem lokalen AirPlay-Bildschirm für
iPhone, iPad und Mac. Die native Windows-Oberfläche startet den
Open-Source-Empfänger [UxPlay](https://github.com/FDH2/UxPlay); Streams und
Geräteerkennung bleiben im lokalen Netzwerk.

## Einfach starten

### Für normale Nutzer

1. Lade unter [Releases](https://github.com/TheGalitube/LocalPlay/releases/latest)
   die Datei `LocalPlay-<Version>-win-x64.zip` herunter.
2. Entpacke die ZIP-Datei vollständig.
3. Starte `LocalPlay.exe`.
4. Starte den Empfänger. Beim ersten Start bietet LocalPlay automatisch an,
   die passenden Windows-Firewall-Regeln einzurichten.
5. Wähle **LocalPlay** auf dem Apple-Gerät.

Das Release ist portabel und selbstständig: .NET, MSYS2 und UxPlay müssen
nicht separat installiert werden. Die EXE ist derzeit nicht digital signiert;
zu jedem Release wird deshalb eine SHA-256-Prüfsumme veröffentlicht.

### Direkt aus dem Repository

Nach `git clone` oder **Code → Download ZIP** genügt ein Doppelklick auf:

```text
Start-LocalPlay.cmd
```

Beim ersten Start werden .NET 8, Git, MSYS2 und die benötigten
Medienkomponenten bei Bedarf eingerichtet. Anschließend wird LocalPlay gebaut
und geöffnet. Dafür werden eine Internetverbindung und `winget` benötigt; der
erste Build kann einige Minuten dauern.

Alternativ aus PowerShell:

```powershell
.\scripts\run.ps1
```

## Verbindung

- **iPhone/iPad:** Kontrollzentrum → Bildschirmsynchronisierung → LocalPlay
- **Mac:** Kontrollzentrum → Bildschirmsynchronisierung → LocalPlay
- **Zweiter Mac-Bildschirm:** Nach der Verbindung gegebenenfalls
  „Als separaten Bildschirm verwenden“ auswählen.

Beide Geräte müssen sich im selben LAN befinden. Gast-WLANs und aktivierte
Client-Isolation blockieren häufig mDNS oder direkte Geräteverbindungen.

## Funktionen

- Bildschirmspiegelung von iPhone, iPad und Mac mit Audio
- Verwendung als zusätzlicher AirPlay-Bildschirm unter macOS
- PIN-Kopplung für neue Geräte
- 1080p, 1440p/2K und 4K mit bis zu 60 FPS
- Wiedergabeprofile für latenzarme Videoschnitt-Arbeit oder A/V-synchrone
  Filmwiedergabe
- automatische oder manuelle Auswahl des IPv4-Netzwerkadapters
- automatische Erneuerung der AirPlay-Ankündigung nach Netzwerkwechseln
- optionaler Hintergrundbetrieb über den Windows-Infobereich
- konfigurierbarer AirPlay-Portbereich
- integrierter Netzwerk- und Porttest
- Windows-Firewall-Regeln für `Private`/`Domain`, optional `Public`, immer nur
  für `LocalSubnet`
- kein Cloud-Konto und kein Medien-Upload

Den Hintergrundbetrieb aktivierst du auf der Seite **Empfänger** mit
**Beim Schließen im Hintergrund weiterlaufen**. Danach blendet der
Schließen-Button das Fenster aus, während der Prozess und ein gestarteter
Empfänger weiterlaufen. Über das Symbol im Windows-Infobereich lässt sich das
Fenster wieder öffnen oder LocalPlay vollständig beenden. Bei einer neuen
Verbindung kommt das normale App-Fenster automatisch wieder nach vorn.

### Flüssiger zweiter Bildschirm für Videoschnitt

Wähle auf **Empfänger** die Qualität **2K · 60 FPS (HEVC)** und das Profil
**Videoschnitt · 2K/60 · geringe Latenz**. Das ist die neue Voreinstellung: UxPlay
zeigt eingehende Frames ohne zusätzliche Zeitstempel-Pufferung an, LocalPlay
verwendet nach einer Laufzeitprüfung bevorzugt Direct3D11-Hardwaredecoding und
fordert beim Mac 2560 × 1440 mit bis zu 60 FPS an. Fehlt ein vollständiger
Direct3D11-HEVC-Pfad, fällt LocalPlay automatisch auf 1080p · 60 FPS und eine
kompatible Decoderwahl zurück, statt 2K über einen überlasteten Softwarepfad zu
erzwingen.

Für reine Filmwiedergabe hält **Video · A/V-synchron** Bild und Ton anhand der
AirPlay-Zeitstempel zusammen. Bei überlastetem WLAN oder Decoder können dabei
verspätete Frames verworfen werden, statt wachsende Verzögerung aufzubauen.

AirPlay bleibt komprimierte, netzwerkabhängige Bildschirmübertragung. Für
flüssige Timeline- und Fensterarbeit sind 5-GHz-/6-GHz-WLAN mit gutem Empfang
oder Ethernet am Windows-PC sinnvoll. Farbverbindliches Grading, garantiert
verlustfreie Farbwiedergabe und die konstant sehr niedrige Latenz eines
für Game-Streaming entwickelten Protokolls kann AirPlay nicht zusichern.

## Grenzen

AirPlay ist ein proprietäres Protokoll. LocalPlay verwendet UxPlays
reverse-engineerte Unterstützung für ältere AirPlay-2-Verbindungen.
DRM-geschützte Videos, Multiroom-Audio, Tastatur-/Mausweiterleitung und
Kompatibilität mit zukünftigen Apple-Versionen sind nicht garantiert.

## Entwicklung

Voraussetzungen für einen manuellen Build:

- Windows 10/11 x64
- PowerShell 5.1 oder neuer
- .NET SDK 8
- MSYS2 UCRT64 mit den in `scripts/bootstrap.ps1` aufgeführten Paketen

```powershell
.\scripts\bootstrap.ps1
.\scripts\test.ps1
.\scripts\package-portable.ps1 -Version 0.2.1
```

Ausgaben:

- Entwicklungsbuild: `artifacts\LocalPlay`
- portables Release: `artifacts\LocalPlay-<Version>-win-x64.zip`
- SHA-256: gleichnamige Datei mit der Endung `.sha256`

Pull Requests werden auf einem frischen Windows-Runner kompiliert. Ein Tag im
Format `v1.2.3` baut automatisch das portable Paket und erstellt ein GitHub
Release:

```powershell
git tag v0.2.1
git push origin v0.2.1
```

Weitere Hinweise stehen in [CONTRIBUTING.md](CONTRIBUTING.md).

## Datenschutz und Sicherheit

LocalPlay lauscht nur lokal und legt Firewall-Regeln ausschließlich für das
lokale Subnetz an. Private und Domänennetzwerke werden standardmäßig
unterstützt. Für ein vertrauenswürdiges Heimnetz, das Windows als „Öffentlich“
eingestuft hat, kann die Freigabe ausdrücklich aktiviert werden. Der Benutzer
muss die Administratorabfrage für diese Regeln bestätigen.

## Wenn LocalPlay nicht auf dem Apple-Gerät erscheint

1. Öffne **Netzwerk** und starte **Netzwerk prüfen**. Der Test kontrolliert
   jetzt Adapter, Windows-Netzwerkprofil, Ports und die tatsächlichen
   Firewall-Regeln.
2. Zeigt Windows das Heimnetz als **Öffentlich**, aktiviere die entsprechende
   Option nur, wenn du diesem LAN vertraust, und richte die Regeln erneut ein.
3. Wähle bei VPN-, VMware- oder Tailscale-Adaptern den echten Ethernet- oder
   WLAN-Adapter manuell aus.
4. Stelle sicher, dass beide Geräte im gleichen Subnetz sind. Gast-WLAN und
   „Client Isolation/AP Isolation“ verhindern direkte Geräteverbindungen und
   können von LocalPlay nicht aufgehoben werden.
5. Drittanbieter-Firewalls müssen `engine\uxplay.exe`, UDP 5353 sowie den
   ausgewählten TCP/UDP-Portbereich zulassen.

Nach einem Wechsel von WLAN oder Ethernet erneuert LocalPlay die gebundene
Adresse und die mDNS-Ankündigung automatisch. Ist vorübergehend kein LAN
verfügbar, wartet die App und versucht die Wiederherstellung regelmäßig erneut.

Sicherheitsprobleme bitte gemäß [SECURITY.md](SECURITY.md) melden. Diagnose-Logs
können lokale IP-Adressen enthalten und sollten vor dem Teilen geprüft werden.

## Lizenzierung

Der LocalPlay-Anwendungscode steht unter der MIT-Lizenz. UxPlay ist GPLv3;
GStreamer und seine Bibliotheken verwenden verschiedene Open-Source-Lizenzen.
Portable Pakete enthalten Hinweise, Lizenztexte, die exakte
UxPlay-Upstream-Revision als Quellarchiv und den angewendeten Patch. Details:
[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

LocalPlay ist weder mit Apple verbunden noch von Apple zertifiziert. Apple und
AirPlay sind Marken von Apple Inc.
