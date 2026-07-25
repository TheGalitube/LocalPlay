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
4. Öffne **Netzwerk**, führe den Verbindungstest aus und aktualisiere einmalig
   die Firewall-Regeln.
5. Starte den Empfänger und wähle **LocalPlay** auf dem Apple-Gerät.

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
- automatische oder manuelle Auswahl des IPv4-Netzwerkadapters
- konfigurierbarer AirPlay-Portbereich
- integrierter Netzwerk- und Porttest
- Windows-Firewall-Regeln nur für `Private` und `LocalSubnet`
- kein Cloud-Konto und kein Medien-Upload

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
.\scripts\package-portable.ps1 -Version 0.2.0
```

Ausgaben:

- Entwicklungsbuild: `artifacts\LocalPlay`
- portables Release: `artifacts\LocalPlay-<Version>-win-x64.zip`
- SHA-256: gleichnamige Datei mit der Endung `.sha256`

Pull Requests werden auf einem frischen Windows-Runner kompiliert. Ein Tag im
Format `v1.2.3` baut automatisch das portable Paket und erstellt ein GitHub
Release:

```powershell
git tag v0.2.0
git push origin v0.2.0
```

Weitere Hinweise stehen in [CONTRIBUTING.md](CONTRIBUTING.md).

## Datenschutz und Sicherheit

LocalPlay lauscht nur lokal und legt Firewall-Regeln ausschließlich für das
private Netzwerkprofil und das lokale Subnetz an. Der Benutzer muss die
Administratorabfrage für diese Regeln ausdrücklich bestätigen.

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
