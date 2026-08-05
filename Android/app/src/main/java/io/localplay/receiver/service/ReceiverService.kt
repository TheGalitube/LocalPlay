package io.localplay.receiver.service

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.app.Service
import android.content.Context
import android.content.Intent
import android.net.wifi.WifiManager
import android.os.IBinder
import android.provider.Settings
import androidx.core.app.NotificationCompat
import io.localplay.receiver.MainActivity
import io.localplay.receiver.engine.NativeReceiverBridge
import io.localplay.receiver.model.ReceiverConfig
import io.localplay.receiver.model.ReceiverPhase
import io.localplay.receiver.state.ReceiverStateStore
import java.io.File
import java.security.MessageDigest
import java.util.concurrent.ExecutorService
import java.util.concurrent.Executors

class ReceiverService : Service() {
    private val executor: ExecutorService = Executors.newSingleThreadExecutor()
    private var multicastLock: WifiManager.MulticastLock? = null
    private var wifiLock: WifiManager.WifiLock? = null
    private lateinit var publisher: AirPlayServicePublisher

    override fun onCreate() {
        super.onCreate()
        publisher = AirPlayServicePublisher(applicationContext)
        createNotificationChannel()
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        when (intent?.action) {
            ACTION_STOP -> stopReceiver()
            ACTION_START -> startReceiver(intent)
        }
        return START_NOT_STICKY
    }

    override fun onBind(intent: Intent?): IBinder? = null

    private fun startReceiver(intent: Intent) {
        val config = ReceiverConfig(
            name = intent.getStringExtra(EXTRA_NAME) ?: "LocalPlay",
            requirePin = intent.getBooleanExtra(EXTRA_REQUIRE_PIN, false),
            portStart = intent.getIntExtra(EXTRA_PORT_START, 7000),
        ).validate()

        startForeground(NOTIFICATION_ID, buildNotification("Empfänger wird gestartet …"))
        acquireNetworkLocks()
        ReceiverStateStore.update(ReceiverPhase.STARTING, "Empfänger wird gestartet …")

        executor.execute {
            val pairingFile = File(filesDir, "pairing-register.plist").absolutePath
            val deviceId = stableDeviceId()
            NativeReceiverBridge.startPlayback()
            val result = NativeReceiverBridge.start(
                config.name,
                deviceId,
                config.requirePin,
                config.portStart,
                pairingFile,
            )
            if (result != NativeReceiverBridge.RESULT_OK) {
                val message = when (result) {
                    NativeReceiverBridge.RESULT_ENGINE_NOT_LINKED ->
                        "Der native UxPlay-Kern ist in diesem Build noch nicht eingebunden."
                    else -> "Der AirPlay-Empfänger konnte nicht gestartet werden (Fehler $result)."
                }
                ReceiverStateStore.update(ReceiverPhase.ERROR, message)
                NativeReceiverBridge.stopPlayback()
                releaseNetworkLocks()
                stopForeground(STOP_FOREGROUND_REMOVE)
                stopSelf()
            } else {
                val port = NativeReceiverBridge.port()
                if (port <= 0) {
                    failStart("Der AirPlay-Kern hat keinen Netzwerkport geöffnet.")
                } else {
                    publisher.register(
                        receiverName = config.name,
                        deviceId = deviceId,
                        port = port,
                        onReady = {
                            ReceiverStateStore.update(
                                ReceiverPhase.ADVERTISING,
                                "Bereit für AirPlay-Verbindungen",
                            )
                            getSystemService(NotificationManager::class.java).notify(
                                NOTIFICATION_ID,
                                buildNotification("Bereit für AirPlay"),
                            )
                        },
                        onError =(::failStart),
                    )
                }
            }
        }
    }

    private fun stopReceiver() {
        executor.execute {
            publisher.unregister()
            NativeReceiverBridge.stop()
            NativeReceiverBridge.stopPlayback()
            ReceiverStateStore.update(ReceiverPhase.STOPPED, "Empfänger ist aus")
            releaseNetworkLocks()
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopSelf()
        }
    }

    private fun failStart(message: String) {
        publisher.unregister()
        NativeReceiverBridge.stop()
        NativeReceiverBridge.stopPlayback()
        ReceiverStateStore.update(ReceiverPhase.ERROR, message)
        releaseNetworkLocks()
        stopForeground(STOP_FOREGROUND_REMOVE)
        stopSelf()
    }

    private fun stableDeviceId(): String {
        val androidId = Settings.Secure.getString(contentResolver, Settings.Secure.ANDROID_ID)
            ?: packageName
        val bytes = MessageDigest.getInstance("SHA-256")
            .digest("$packageName:$androidId".toByteArray())
            .copyOf(6)
        bytes[0] = ((bytes[0].toInt() and 0xfc) or 0x02).toByte()
        return bytes.joinToString(":") { "%02X".format(it.toInt() and 0xff) }
    }

    private fun acquireNetworkLocks() {
        val wifiManager = applicationContext.getSystemService(Context.WIFI_SERVICE) as WifiManager
        multicastLock = wifiManager.createMulticastLock("LocalPlay:mDNS").apply {
            setReferenceCounted(false)
            acquire()
        }
        @Suppress("DEPRECATION")
        wifiLock = wifiManager.createWifiLock(WifiManager.WIFI_MODE_FULL_HIGH_PERF, "LocalPlay:stream").apply {
            setReferenceCounted(false)
            acquire()
        }
    }

    private fun releaseNetworkLocks() {
        multicastLock?.takeIf { it.isHeld }?.release()
        multicastLock = null
        wifiLock?.takeIf { it.isHeld }?.release()
        wifiLock = null
    }

    private fun createNotificationChannel() {
        val channel = NotificationChannel(
            CHANNEL_ID,
            "AirPlay-Empfänger",
            NotificationManager.IMPORTANCE_LOW,
        ).apply {
            description = "Zeigt an, solange LocalPlay im lokalen Netzwerk erreichbar ist."
        }
        getSystemService(NotificationManager::class.java).createNotificationChannel(channel)
    }

    private fun buildNotification(text: String): Notification {
        val openIntent = PendingIntent.getActivity(
            this,
            0,
            Intent(this, MainActivity::class.java),
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE,
        )
        val stopIntent = PendingIntent.getService(
            this,
            1,
            Intent(this, ReceiverService::class.java).setAction(ACTION_STOP),
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE,
        )
        return NotificationCompat.Builder(this, CHANNEL_ID)
            .setSmallIcon(android.R.drawable.ic_media_play)
            .setContentTitle("LocalPlay")
            .setContentText(text)
            .setContentIntent(openIntent)
            .setOngoing(true)
            .addAction(android.R.drawable.ic_media_pause, "Beenden", stopIntent)
            .build()
    }

    override fun onDestroy() {
        publisher.unregister()
        NativeReceiverBridge.stop()
        NativeReceiverBridge.stopPlayback()
        releaseNetworkLocks()
        executor.shutdownNow()
        super.onDestroy()
    }

    companion object {
        private const val CHANNEL_ID = "localplay_receiver"
        private const val NOTIFICATION_ID = 1701
        private const val ACTION_START = "io.localplay.receiver.START"
        private const val ACTION_STOP = "io.localplay.receiver.STOP"
        private const val EXTRA_NAME = "receiver_name"
        private const val EXTRA_REQUIRE_PIN = "require_pin"
        private const val EXTRA_PORT_START = "port_start"

        fun startIntent(context: Context, config: ReceiverConfig): Intent =
            Intent(context, ReceiverService::class.java)
                .setAction(ACTION_START)
                .putExtra(EXTRA_NAME, config.name)
                .putExtra(EXTRA_REQUIRE_PIN, config.requirePin)
                .putExtra(EXTRA_PORT_START, config.portStart)

        fun stopIntent(context: Context): Intent =
            Intent(context, ReceiverService::class.java).setAction(ACTION_STOP)
    }
}
