#pragma once

#include <android/native_window.h>
#include <string>

namespace localplay {

constexpr int kResultOk = 0;
constexpr int kResultEngineNotLinked = 1001;

struct ReceiverConfig {
    std::string name;
    std::string device_id;
    bool require_pin;
    int port_start;
    std::string pairing_file;
};

class ReceiverEngine {
public:
    bool is_ready() const;
    int start(const ReceiverConfig& config);
    int port() const;
    void stop();
    void set_surface(ANativeWindow* surface);
};

ReceiverEngine& receiver_engine();

}  // namespace localplay
