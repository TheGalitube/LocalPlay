# Drittanbieter-Hinweise für LocalPlay Android

Der Android-Empfängerkern basiert auf
[`dsafa22/AirplayServer`](https://github.com/dsafa22/AirplayServer), Revision
`5ba1b6965c5b3ab835c8041b10fa0b2fb91a2f6f`, mit Android-spezifischen
Änderungen durch LocalPlay.

Die übernommenen Quellen enthalten Komponenten unter unterschiedlichen
Lizenzen:

- AirPlayServer-Kern: LGPL 2.1 oder später
- PlayFair: GNU GPLv3
- plist: LGPL 2.1 oder später
- Ed25519 und HTTP-Parser: MIT
- Crypto und Curve25519: BSD-artige Lizenzen
- Fraunhofer FDK AAC: eigene FDK-AAC-Lizenz mit zusätzlichen Bedingungen

Die maßgeblichen Texte befinden sich zusammen mit den entsprechenden Quellen:

- `app/src/main/cpp/airplayserver/LICENSE`
- `app/src/main/cpp/airplayserver/lib/playfair/LICENSE.md`
- `app/src/main/cpp/airplayserver/third_party/fdk-aac/NOTICE`

Vor einer öffentlichen oder kommerziellen APK-Verteilung müssen insbesondere
die Quellcode-, Hinweis- und Patentbedingungen der kombinierten Komponenten
geprüft und erfüllt werden. LocalPlay und AirPlayServer sind weder mit Apple
verbunden noch von Apple zertifiziert. Apple und AirPlay sind Marken von Apple
Inc.
