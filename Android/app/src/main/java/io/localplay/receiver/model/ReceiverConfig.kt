package io.localplay.receiver.model

data class ReceiverConfig(
    val name: String = "LocalPlay",
    val requirePin: Boolean = false,
    val portStart: Int = 7000,
) {
    fun validate(): ReceiverConfig {
        val normalizedName = name.trim()
        require(normalizedName.isNotEmpty()) { "Der Empfängername darf nicht leer sein." }
        require(normalizedName.length <= 40) { "Der Empfängername darf höchstens 40 Zeichen haben." }
        require(portStart in 1024..65533) { "Der Startport muss zwischen 1024 und 65533 liegen." }
        return copy(name = normalizedName)
    }
}
