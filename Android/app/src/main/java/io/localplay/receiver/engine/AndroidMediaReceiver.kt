package io.localplay.receiver.engine

import android.media.AudioAttributes
import android.media.AudioFormat
import android.media.AudioManager
import android.media.AudioTrack
import android.media.MediaCodec
import android.media.MediaFormat
import android.os.Process
import android.util.Log
import android.view.Surface
import java.util.concurrent.LinkedBlockingDeque
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicBoolean

internal class AndroidMediaReceiver(
    private val onError: (String) -> Unit,
    private val onFirstVideoFrame: () -> Unit,
) {
    private data class VideoPacket(
        val data: ByteArray,
        val presentationTimeUs: Long,
        val width: Int,
        val height: Int,
        val isKeyFrame: Boolean,
    )

    private data class AudioPacket(val samples: ShortArray)

    private val running = AtomicBoolean(false)
    private val videoQueue = LinkedBlockingDeque<VideoPacket>(VIDEO_QUEUE_CAPACITY)
    private val audioQueue = LinkedBlockingDeque<AudioPacket>(AUDIO_QUEUE_CAPACITY)

    @Volatile
    private var surface: Surface? = null

    @Volatile
    private var resetVideo = false

    private var videoThread: Thread? = null
    private var audioThread: Thread? = null

    fun start() {
        if (!running.compareAndSet(false, true)) return
        videoThread = Thread(::videoLoop, "LocalPlay-video").also { it.start() }
        audioThread = Thread(::audioLoop, "LocalPlay-audio").also { it.start() }
    }

    fun stop() {
        if (!running.compareAndSet(true, false)) return
        videoThread?.interrupt()
        audioThread?.interrupt()
        videoThread = null
        audioThread = null
        videoQueue.clear()
        audioQueue.clear()
        surface = null
    }

    fun setSurface(value: Surface?) {
        surface = value
        resetVideo = true
    }

    fun resetVideo() {
        videoQueue.clear()
        resetVideo = true
    }

    fun offerVideo(
        data: ByteArray,
        presentationTimeUs: Long,
        width: Int,
        height: Int,
        isKeyFrame: Boolean,
    ) {
        if (!running.get()) return
        val packet = VideoPacket(
            data = data,
            presentationTimeUs = presentationTimeUs,
            width = width.takeIf { it > 0 } ?: DEFAULT_WIDTH,
            height = height.takeIf { it > 0 } ?: DEFAULT_HEIGHT,
            isKeyFrame = isKeyFrame,
        )
        if (!videoQueue.offerLast(packet)) {
            videoQueue.pollFirst()
            videoQueue.offerLast(packet)
        }
    }

    fun offerAudio(samples: ShortArray) {
        if (!running.get()) return
        val packet = AudioPacket(samples)
        if (!audioQueue.offerLast(packet)) {
            audioQueue.pollFirst()
            audioQueue.offerLast(packet)
        }
    }

    private fun videoLoop() {
        Process.setThreadPriority(Process.THREAD_PRIORITY_DISPLAY)
        var codec: MediaCodec? = null
        var codecSurface: Surface? = null
        var width = 0
        var height = 0
        var firstPresentationTimeUs = Long.MIN_VALUE
        var renderedFirstFrame = false
        val outputInfo = MediaCodec.BufferInfo()

        fun releaseCodec() {
            try {
                codec?.stop()
            } catch (_: Exception) {
                // A decoder may already be stopped after a surface disappears.
            }
            try {
                codec?.release()
            } catch (_: Exception) {
                // Nothing else to release.
            }
            codec = null
            codecSurface = null
            firstPresentationTimeUs = Long.MIN_VALUE
            renderedFirstFrame = false
        }

        try {
            while (running.get()) {
                val targetSurface = surface
                if (targetSurface == null || !targetSurface.isValid) {
                    Thread.sleep(SURFACE_RETRY_MS)
                    continue
                }

                val packet = videoQueue.poll(100, TimeUnit.MILLISECONDS) ?: continue

                if (resetVideo || codec == null || codecSurface !== targetSurface ||
                    width != packet.width || height != packet.height
                ) {
                    resetVideo = false
                    releaseCodec()
                    if (!packet.isKeyFrame) {
                        Log.d(TAG, "Warte nach Surface-Wechsel auf den nächsten H.264-Keyframe")
                        continue
                    }
                    width = packet.width
                    height = packet.height
                    codecSurface = targetSurface
                    codec = MediaCodec.createDecoderByType(MediaFormat.MIMETYPE_VIDEO_AVC).apply {
                        val format = MediaFormat.createVideoFormat(
                            MediaFormat.MIMETYPE_VIDEO_AVC,
                            width,
                            height,
                        ).apply {
                            setInteger(MediaFormat.KEY_MAX_INPUT_SIZE, MAX_VIDEO_PACKET_BYTES)
                            setInteger(MediaFormat.KEY_PRIORITY, 0)
                        }
                        configure(format, targetSurface, null, 0)
                        setVideoScalingMode(MediaCodec.VIDEO_SCALING_MODE_SCALE_TO_FIT)
                        start()
                    }
                    Log.i(TAG, "H.264-Decoder gestartet: ${width}x$height")
                }

                val activeCodec = codec ?: continue
                val inputIndex = activeCodec.dequeueInputBuffer(CODEC_TIMEOUT_US)
                if (inputIndex >= 0) {
                    val input = activeCodec.getInputBuffer(inputIndex)
                    if (input != null && packet.data.size <= input.capacity()) {
                        input.clear()
                        input.put(packet.data)
                        if (firstPresentationTimeUs == Long.MIN_VALUE) {
                            firstPresentationTimeUs = packet.presentationTimeUs
                        }
                        val normalizedPts = (packet.presentationTimeUs - firstPresentationTimeUs)
                            .coerceAtLeast(0L)
                        activeCodec.queueInputBuffer(
                            inputIndex,
                            0,
                            packet.data.size,
                            normalizedPts,
                            if (packet.isKeyFrame) MediaCodec.BUFFER_FLAG_KEY_FRAME else 0,
                        )
                    } else {
                        activeCodec.queueInputBuffer(inputIndex, 0, 0, 0, 0)
                    }
                } else {
                    // The packet has already been removed from the queue. Put it back so a busy
                    // decoder can never lose the initial SPS/PPS + IDR access unit.
                    videoQueue.offerFirst(packet)
                }

                var outputIndex = activeCodec.dequeueOutputBuffer(outputInfo, 0)
                while (outputIndex >= 0) {
                    activeCodec.releaseOutputBuffer(outputIndex, true)
                    if (!renderedFirstFrame) {
                        renderedFirstFrame = true
                        Log.i(TAG, "Ersten H.264-Frame an Surface ausgegeben")
                        onFirstVideoFrame()
                    }
                    outputIndex = activeCodec.dequeueOutputBuffer(outputInfo, 0)
                }
            }
        } catch (_: InterruptedException) {
            // Normal shutdown.
        } catch (error: Exception) {
            Log.e(TAG, "H.264-Wiedergabe fehlgeschlagen", error)
            if (running.get()) onError("Video konnte nicht wiedergegeben werden: ${error.message}")
        } finally {
            releaseCodec()
        }
    }

    private fun audioLoop() {
        Process.setThreadPriority(Process.THREAD_PRIORITY_AUDIO)
        val minimumBuffer = AudioTrack.getMinBufferSize(
            AUDIO_SAMPLE_RATE,
            AudioFormat.CHANNEL_OUT_STEREO,
            AudioFormat.ENCODING_PCM_16BIT,
        ).coerceAtLeast(AUDIO_SAMPLE_RATE / 5 * 4)

        var track: AudioTrack? = null
        try {
            track = AudioTrack.Builder()
                .setAudioAttributes(
                    AudioAttributes.Builder()
                        .setUsage(AudioAttributes.USAGE_MEDIA)
                        .setContentType(AudioAttributes.CONTENT_TYPE_MOVIE)
                        .build(),
                )
                .setAudioFormat(
                    AudioFormat.Builder()
                        .setEncoding(AudioFormat.ENCODING_PCM_16BIT)
                        .setSampleRate(AUDIO_SAMPLE_RATE)
                        .setChannelMask(AudioFormat.CHANNEL_OUT_STEREO)
                        .build(),
                )
                .setTransferMode(AudioTrack.MODE_STREAM)
                .setBufferSizeInBytes(minimumBuffer)
                .setPerformanceMode(AudioTrack.PERFORMANCE_MODE_LOW_LATENCY)
                .build()
            track.play()
            while (running.get()) {
                val packet = audioQueue.poll(100, TimeUnit.MILLISECONDS) ?: continue
                track.write(packet.samples, 0, packet.samples.size, AudioTrack.WRITE_BLOCKING)
            }
        } catch (_: InterruptedException) {
            // Normal shutdown.
        } catch (error: Exception) {
            Log.e(TAG, "PCM-Wiedergabe fehlgeschlagen", error)
            if (running.get()) onError("Audio konnte nicht wiedergegeben werden: ${error.message}")
        } finally {
            try {
                track?.stop()
            } catch (_: Exception) {
                // Nothing else to stop.
            }
            track?.release()
        }
    }

    private companion object {
        const val AUDIO_SAMPLE_RATE = 44_100
        const val AUDIO_QUEUE_CAPACITY = 80
        const val VIDEO_QUEUE_CAPACITY = 90
        const val CODEC_TIMEOUT_US = 10_000L
        const val SURFACE_RETRY_MS = 16L
        const val DEFAULT_WIDTH = 640
        const val DEFAULT_HEIGHT = 360
        const val MAX_VIDEO_PACKET_BYTES = 4 * 1024 * 1024
        const val TAG = "LocalPlayMedia"
    }
}
