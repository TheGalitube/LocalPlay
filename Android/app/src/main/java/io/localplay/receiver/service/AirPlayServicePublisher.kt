package io.localplay.receiver.service

import android.content.Context
import android.net.nsd.NsdManager
import android.net.nsd.NsdServiceInfo
import java.util.concurrent.atomic.AtomicBoolean
import java.util.concurrent.atomic.AtomicInteger

internal class AirPlayServicePublisher(context: Context) {
    private val nsdManager = context.getSystemService(NsdManager::class.java)
    private val active = AtomicBoolean(false)
    private var raopListener: NsdManager.RegistrationListener? = null
    private var airPlayListener: NsdManager.RegistrationListener? = null

    fun register(
        receiverName: String,
        deviceId: String,
        port: Int,
        onReady: () -> Unit,
        onError: (String) -> Unit,
    ) {
        unregister()
        active.set(true)
        val completed = AtomicInteger(0)
        val failed = AtomicBoolean(false)

        fun listener(label: String) = object : NsdManager.RegistrationListener {
            override fun onServiceRegistered(serviceInfo: NsdServiceInfo) {
                if (active.get() && !failed.get() && completed.incrementAndGet() == 2) onReady()
            }

            override fun onRegistrationFailed(serviceInfo: NsdServiceInfo, errorCode: Int) {
                if (active.get() && failed.compareAndSet(false, true)) {
                    onError("$label konnte nicht im Netzwerk veröffentlicht werden (Fehler $errorCode).")
                }
            }

            override fun onServiceUnregistered(serviceInfo: NsdServiceInfo) = Unit
            override fun onUnregistrationFailed(serviceInfo: NsdServiceInfo, errorCode: Int) = Unit
        }

        raopListener = listener("RAOP").also {
            nsdManager.registerService(
                raopInfo(receiverName, deviceId, port),
                NsdManager.PROTOCOL_DNS_SD,
                it,
            )
        }
        airPlayListener = listener("AirPlay").also {
            nsdManager.registerService(
                airPlayInfo(receiverName, deviceId, port),
                NsdManager.PROTOCOL_DNS_SD,
                it,
            )
        }
    }

    fun unregister() {
        active.set(false)
        listOfNotNull(raopListener, airPlayListener).forEach { listener ->
            try {
                nsdManager.unregisterService(listener)
            } catch (_: IllegalArgumentException) {
                // The registration may already have failed or been removed by Android.
            }
        }
        raopListener = null
        airPlayListener = null
    }

    private fun raopInfo(receiverName: String, deviceId: String, port: Int) =
        NsdServiceInfo().apply {
            serviceName = "${deviceId.replace(":", "")}@$receiverName"
            serviceType = "_raop._tcp"
            this.port = port
            attributes(
                "txtvers" to "1",
                "ch" to "2",
                "cn" to "0,1,2,3",
                "et" to "0,3,5",
                "vv" to "2",
                "ft" to FEATURES,
                "am" to MODEL,
                "md" to "0,1,2",
                "rhd" to "5.6.0.0",
                "pw" to "false",
                "sr" to "44100",
                "ss" to "16",
                "sv" to "false",
                "tp" to "UDP",
                "da" to "true",
                "sf" to "0x4",
                "vs" to SOURCE_VERSION,
                "vn" to "65537",
                "pk" to PUBLIC_KEY,
            )
        }

    private fun airPlayInfo(receiverName: String, deviceId: String, port: Int) =
        NsdServiceInfo().apply {
            serviceName = receiverName
            serviceType = "_airplay._tcp"
            this.port = port
            attributes(
                "deviceid" to deviceId,
                "features" to FEATURES,
                "srcvers" to SOURCE_VERSION,
                "flags" to "0x4",
                "vv" to "2",
                "model" to MODEL,
                "rhd" to "5.6.0.0",
                "pw" to "false",
                "pk" to PUBLIC_KEY,
                "pi" to "2e388006-13ba-4041-9a67-25dd4a43d536",
            )
        }

    private fun NsdServiceInfo.attributes(vararg values: Pair<String, String>) {
        values.forEach { (key, value) -> setAttribute(key, value) }
    }

    private companion object {
        const val MODEL = "AppleTV2,1"
        const val FEATURES = "0x5A7FFFF7,0x1E"
        const val SOURCE_VERSION = "220.68"
        const val PUBLIC_KEY =
            "b07727d6f6cd6e08b58ede525ec3cdeaa252ad9f683feb212ef8a205246554e7"
    }
}
