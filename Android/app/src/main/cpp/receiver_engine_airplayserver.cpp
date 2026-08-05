#include "receiver_engine.hpp"

#include <jni.h>

#include <array>
#include <atomic>
#include <cstdio>
#include <cstdlib>
#include <mutex>
#include <string>

extern "C" {
#include "raop_server.h"
}

namespace localplay {
namespace {

JavaVM* java_vm = nullptr;
jclass bridge_class = nullptr;
jmethodID state_method = nullptr;
jmethodID video_method = nullptr;
jmethodID audio_method = nullptr;

std::mutex engine_mutex;
raop_server_t* server = nullptr;
std::atomic<int> server_port{0};
std::atomic<bool> video_connected{false};

class ScopedEnv {
public:
    ScopedEnv() {
        if (java_vm == nullptr) return;
        if (java_vm->GetEnv(reinterpret_cast<void**>(&env_), JNI_VERSION_1_6) != JNI_OK) {
            if (java_vm->AttachCurrentThread(&env_, nullptr) == JNI_OK) {
                attached_ = true;
            }
        }
    }

    ~ScopedEnv() {
        if (attached_ && java_vm != nullptr) java_vm->DetachCurrentThread();
    }

    JNIEnv* get() const { return env_; }

private:
    JNIEnv* env_ = nullptr;
    bool attached_ = false;
};

void report_state(int state, const char* message) {
    ScopedEnv scoped;
    JNIEnv* env = scoped.get();
    if (env == nullptr || bridge_class == nullptr || state_method == nullptr) return;
    jstring java_message = env->NewStringUTF(message);
    env->CallStaticVoidMethod(bridge_class, state_method, state, java_message);
    env->DeleteLocalRef(java_message);
}

std::array<char, 6> parse_device_id(const std::string& value) {
    std::array<char, 6> result{0x02, 0x4c, 0x50, 0x00, 0x00, 0x01};
    unsigned int bytes[6]{};
    if (std::sscanf(
            value.c_str(),
            "%02x:%02x:%02x:%02x:%02x:%02x",
            &bytes[0], &bytes[1], &bytes[2], &bytes[3], &bytes[4], &bytes[5]) == 6) {
        for (size_t index = 0; index < result.size(); ++index) {
            result[index] = static_cast<char>(bytes[index] & 0xffU);
        }
    }
    return result;
}

void on_audio(void*, void* opaque, pcm_data_struct* data) {
    if (data == nullptr || data->data == nullptr || data->data_len <= 0) return;
    auto* session = static_cast<audio_session_t*>(opaque);
    if (session != nullptr && session->volume != 1.0f) {
        for (int index = 0; index < data->data_len; ++index) {
            data->data[index] = static_cast<short>(data->data[index] * session->volume);
        }
    }

    ScopedEnv scoped;
    JNIEnv* env = scoped.get();
    if (env == nullptr || bridge_class == nullptr || audio_method == nullptr) return;
    jshortArray samples = env->NewShortArray(data->data_len);
    if (samples == nullptr) return;
    env->SetShortArrayRegion(samples, 0, data->data_len, data->data);
    env->CallStaticVoidMethod(
        bridge_class,
        audio_method,
        samples,
        static_cast<jlong>(data->pts));
    env->DeleteLocalRef(samples);
}

void on_video(void*, h264_decode_struct* data) {
    if (data == nullptr || data->data == nullptr || data->data_len <= 0) return;
    if (!video_connected.exchange(true)) {
        report_state(3, "AirPlay-Gerät verbunden");
    }

    ScopedEnv scoped;
    JNIEnv* env = scoped.get();
    if (env != nullptr && bridge_class != nullptr && video_method != nullptr) {
        jbyteArray nal = env->NewByteArray(data->data_len);
        if (nal != nullptr) {
            env->SetByteArrayRegion(
                nal,
                0,
                data->data_len,
                reinterpret_cast<const jbyte*>(data->data));
            env->CallStaticVoidMethod(
                bridge_class,
                video_method,
                nal,
                static_cast<jint>(data->frame_type),
                static_cast<jlong>(data->pts),
                static_cast<jint>(data->width),
                static_cast<jint>(data->height));
            env->DeleteLocalRef(nal);
        }
    }
    std::free(data->data);
    data->data = nullptr;
}

void on_video_destroy(void*) {
    video_connected = false;
    report_state(2, "Bereit für AirPlay-Verbindungen");
}

}  // namespace

bool ReceiverEngine::is_ready() const {
    return true;
}

int ReceiverEngine::start(const ReceiverConfig& config) {
    std::lock_guard<std::mutex> lock(engine_mutex);
    if (server != nullptr) return kResultOk;

    report_state(1, "AirPlay-Kern wird gestartet …");
    server = raop_server_init(nullptr, on_audio, on_video, on_video_destroy);
    if (server == nullptr) return 2001;

    const auto device_id = parse_device_id(config.device_id);
    const int result = raop_server_start(
        server,
        config.name.c_str(),
        const_cast<char*>(device_id.data()),
        static_cast<int>(device_id.size()));
    if (result != RAOP_SERVER_NOERROR) {
        raop_server_destroy(server);
        server = nullptr;
        return 2100 + result;
    }

    server_port = raop_server_get_port(server);
    video_connected = false;
    return kResultOk;
}

int ReceiverEngine::port() const {
    return server_port.load();
}

void ReceiverEngine::stop() {
    std::lock_guard<std::mutex> lock(engine_mutex);
    if (server != nullptr) {
        raop_server_destroy(server);
        server = nullptr;
    }
    server_port = 0;
    video_connected = false;
}

void ReceiverEngine::set_surface(ANativeWindow*) {
    // Video is decoded by Android MediaCodec on the Kotlin side.
}

ReceiverEngine& receiver_engine() {
    static ReceiverEngine engine;
    return engine;
}

}  // namespace localplay

extern "C" JNIEXPORT jint JNICALL JNI_OnLoad(JavaVM* vm, void*) {
    localplay::java_vm = vm;
    JNIEnv* env = nullptr;
    if (vm->GetEnv(reinterpret_cast<void**>(&env), JNI_VERSION_1_6) != JNI_OK) {
        return JNI_ERR;
    }
    jclass local_class = env->FindClass("io/localplay/receiver/engine/NativeReceiverBridge");
    if (local_class == nullptr) return JNI_ERR;
    localplay::bridge_class = static_cast<jclass>(env->NewGlobalRef(local_class));
    env->DeleteLocalRef(local_class);
    localplay::state_method = env->GetStaticMethodID(
        localplay::bridge_class,
        "onNativeStateChanged",
        "(ILjava/lang/String;)V");
    localplay::video_method = env->GetStaticMethodID(
        localplay::bridge_class,
        "onNativeVideoData",
        "([BIJII)V");
    localplay::audio_method = env->GetStaticMethodID(
        localplay::bridge_class,
        "onNativeAudioData",
        "([SJ)V");
    if (localplay::state_method == nullptr ||
        localplay::video_method == nullptr ||
        localplay::audio_method == nullptr) {
        return JNI_ERR;
    }
    return JNI_VERSION_1_6;
}
