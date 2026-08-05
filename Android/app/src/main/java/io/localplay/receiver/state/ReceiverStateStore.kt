package io.localplay.receiver.state

import io.localplay.receiver.model.ReceiverPhase
import io.localplay.receiver.model.ReceiverStatus
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow

object ReceiverStateStore {
    private val mutableStatus = MutableStateFlow(ReceiverStatus())
    val status: StateFlow<ReceiverStatus> = mutableStatus.asStateFlow()

    fun update(
        phase: ReceiverPhase,
        message: String,
        pin: String? = null,
        clientName: String? = null,
    ) {
        mutableStatus.value = ReceiverStatus(phase, message, pin, clientName)
    }
}
