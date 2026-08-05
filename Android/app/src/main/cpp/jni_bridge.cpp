#include <jni.h>
#include <android/native_window_jni.h>

#include <string>

#include "receiver_engine.hpp"

namespace {
std::string to_string(JNIEnv* env, jstring value) {
    if (value == nullptr) {
        return {};
    }
    const char* chars = env->GetStringUTFChars(value, nullptr);
    std::string result(chars != nullptr ? chars : "");
    if (chars != nullptr) {
        env->ReleaseStringUTFChars(value, chars);
    }
    return result;
}
}  // namespace

extern "C" JNIEXPORT jboolean JNICALL
Java_io_localplay_receiver_engine_NativeReceiverBridge_isEngineReady(
    JNIEnv*,
    jobject) {
    return localplay::receiver_engine().is_ready() ? JNI_TRUE : JNI_FALSE;
}

extern "C" JNIEXPORT jint JNICALL
Java_io_localplay_receiver_engine_NativeReceiverBridge_start(
    JNIEnv* env,
    jobject,
    jstring receiver_name,
    jstring device_id,
    jboolean require_pin,
    jint port_start,
    jstring pairing_file) {
    localplay::ReceiverConfig config{
        to_string(env, receiver_name),
        to_string(env, device_id),
        require_pin == JNI_TRUE,
        static_cast<int>(port_start),
        to_string(env, pairing_file),
    };
    return localplay::receiver_engine().start(config);
}

extern "C" JNIEXPORT jint JNICALL
Java_io_localplay_receiver_engine_NativeReceiverBridge_port(JNIEnv*, jobject) {
    return localplay::receiver_engine().port();
}

extern "C" JNIEXPORT void JNICALL
Java_io_localplay_receiver_engine_NativeReceiverBridge_stop(JNIEnv*, jobject) {
    localplay::receiver_engine().stop();
}

extern "C" JNIEXPORT void JNICALL
Java_io_localplay_receiver_engine_NativeReceiverBridge_setSurface(
    JNIEnv* env,
    jobject,
    jobject surface) {
    ANativeWindow* window = surface != nullptr
        ? ANativeWindow_fromSurface(env, surface)
        : nullptr;
    localplay::receiver_engine().set_surface(window);
    if (window != nullptr) {
        ANativeWindow_release(window);
    }
}
