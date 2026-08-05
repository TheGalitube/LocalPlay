# Native AirPlay engine

`app/src/main/cpp/receiver_engine.hpp` is the stable boundary between Android
and the receiver protocol implementation. The checked-in implementation is a
safe stub so an APK never advertises a receiver that cannot accept a session.

The evaluated upstream baseline is UxPlay commit
`11fa5e38151c6bd8ac8fbd0e5b396484e4e3066e` from 2026-08-04. UxPlay is GPLv3;
its current `lib/` needs OpenSSL 3, libplist, pthreads and an Android-specific
mDNS interface binding. Do not silently fall back to the 2019 Android
AirplayServer: that code only documents iOS 9–13 compatibility.

The replacement engine must implement:

- non-blocking `start` with a fully initialized RTSP/RAOP server;
- idempotent, thread-safe `stop` that joins native worker threads;
- connection, PIN and error callbacks to `NativeReceiverBridge`;
- H.264 first, with HEVC advertised only after a device capability check;
- rendering to the most recently supplied `ANativeWindow`;
- audio output through `AudioTrack` or AAudio;
- pairing data only at the path supplied by the foreground service.
