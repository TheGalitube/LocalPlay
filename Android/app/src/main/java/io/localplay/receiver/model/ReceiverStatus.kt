package io.localplay.receiver.model

enum class ReceiverPhase {
    STOPPED,
    STARTING,
    ADVERTISING,
    CONNECTED,
    ERROR,
}

data class ReceiverStatus(
    val phase: ReceiverPhase = ReceiverPhase.STOPPED,
    val message: String = "Empfänger ist aus",
    val pin: String? = null,
    val clientName: String? = null,
) {
    val isRunning: Boolean
        get() = phase == ReceiverPhase.STARTING ||
            phase == ReceiverPhase.ADVERTISING ||
            phase == ReceiverPhase.CONNECTED
}
