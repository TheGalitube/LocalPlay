package io.localplay.receiver.data

import android.content.Context
import io.localplay.receiver.model.ReceiverConfig

class ReceiverPreferences(context: Context) {
    private val preferences = context.getSharedPreferences("receiver", Context.MODE_PRIVATE)

    fun load(): ReceiverConfig = ReceiverConfig(
        name = preferences.getString(KEY_NAME, "LocalPlay") ?: "LocalPlay",
        requirePin = preferences.getBoolean(KEY_REQUIRE_PIN, false),
        portStart = preferences.getInt(KEY_PORT_START, 7000),
    )

    fun save(config: ReceiverConfig) {
        preferences.edit()
            .putString(KEY_NAME, config.name)
            .putBoolean(KEY_REQUIRE_PIN, config.requirePin)
            .putInt(KEY_PORT_START, config.portStart)
            .apply()
    }

    private companion object {
        const val KEY_NAME = "name"
        const val KEY_REQUIRE_PIN = "require_pin"
        const val KEY_PORT_START = "port_start"
    }
}
