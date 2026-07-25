# Third-party notices

## UxPlay

LocalPlay launches UxPlay as a separate process to provide AirPlay receiving.

- Project: https://github.com/FDH2/UxPlay
- License: GNU General Public License v3.0
- Pinned source revision: `acfb5494fb2b52ca358e62ef59d6ee0ab20dec49`

LocalPlay applies a small Windows-only patch to UxPlay's experimental internal
mDNS responder. It selects an active IPv4 LAN adapter with a gateway instead
of relying on Windows' route choice for `224.0.0.251`, which can incorrectly
resolve to loopback on hosts with VPN or virtual adapters. It also removes the
legacy Winsock 1 import so the Winsock 2 multicast constants used by the source
match the runtime implementation. The patch source is included in
`patches/uxplay-windows-mdns-interface.patch`.

UxPlay includes or derives from additional projects documented in its own
README and license files, including RPiPlay, ShairPlay, PlayFair, llhttp, and
other components. A binary distribution must include UxPlay's corresponding
license and source offer/source code as required by GPLv3.

UxPlay's README notes that its AirPlay implementation is reverse-engineered
from public information and that the legal status of its bundled FairPlay
implementation is unclear. LocalPlay does not claim Apple certification or
MFi approval.

## GStreamer and MSYS2 runtime libraries

The Windows receiver uses GStreamer and supporting libraries supplied by
MSYS2. Their licenses vary by component and plugin. Redistribution must retain
the license notices supplied by those projects and must be reviewed for the
selected plugin set and distribution territory.

Apple and AirPlay are trademarks of Apple Inc. LocalPlay is not affiliated
with or endorsed by Apple.
