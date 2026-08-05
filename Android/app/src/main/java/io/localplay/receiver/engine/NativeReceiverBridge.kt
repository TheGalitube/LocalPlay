package io.localplay.receiver.engine

import android.view.Surface
import io.localplay.receiver.model.ReceiverPhase
import io.localplay.receiver.state.ReceiverStateStore
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow

object NativeReceiverBridge {
    const val RESULT_OK = 0
    const val RESULT_ENGINE_NOT_LINKED = 1001

    init {
        System.loadLibrary("localplay_android")
    }

    external fun isEngineReady(): Boolean

    external fun start(
        receiverName: String,
        deviceId: String,
        requirePin: Boolean,
        portStart: Int,
        pairingFile: String,
    ): Int

    external fun port(): Int

    external fun stop()

    private val mutablePlaybackReady = MutableStateFlow(false)
    val playbackReady = mutablePlaybackReady.asStateFlow()

    private val mediaReceiver = AndroidMediaReceiver(
        onError = ::onPlaybackError,
        onFirstVideoFrame = { mutablePlaybackReady.value = true },
    )

    fun startPlayback() {
        mutablePlaybackReady.value = false
        mediaReceiver.start()
    }

    fun stopPlayback() {
        mutablePlaybackReady.value = false
        mediaReceiver.stop()
    }

    fun setSurface(surface: Surface?) = mediaReceiver.setSurface(surface)

    @JvmStatic
    fun onNativeStateChanged(state: Int, message: String) {
        val phase = when (state) {
            1 -> ReceiverPhase.STARTING
            2 -> ReceiverPhase.ADVERTISING
            3 -> ReceiverPhase.CONNECTED
            else -> ReceiverPhase.ERROR
        }
        if (phase == ReceiverPhase.CONNECTED || phase == ReceiverPhase.ADVERTISING) {
            mutablePlaybackReady.value = false
        }
        if (phase == ReceiverPhase.ADVERTISING) mediaReceiver.resetVideo()
        ReceiverStateStore.update(phase, message)
    }

    @JvmStatic
    fun onNativeVideoData(
        data: ByteArray,
        frameType: Int,
        presentationTimeUs: Long,
        width: Int,
        height: Int,
    ) {
        mediaReceiver.offerVideo(
            data = data,
            presentationTimeUs = presentationTimeUs,
            width = width,
            height = height,
            isKeyFrame = frameType == 5,
        )
    }

    @JvmStatic
    fun onNativeAudioData(samples: ShortArray, presentationTimeUs: Long) {
        mediaReceiver.offerAudio(samples)
    }

    private fun onPlaybackError(message: String) {
        val current = ReceiverStateStore.status.value
        ReceiverStateStore.update(current.phase, message)
    }

    @JvmStatic
    fun onNativePin(pin: String) {
        val current = ReceiverStateStore.status.value
        ReceiverStateStore.update(current.phase, "PIN auf dem Apple-Gerät eingeben", pin)
    }
}
