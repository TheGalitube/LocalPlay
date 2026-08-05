cmake_minimum_required(VERSION 3.22.1)

project(localplay_android LANGUAGES C CXX)

add_subdirectory(airplayserver/lib airplayserver-build)

add_library(
    localplay_android
    SHARED
    jni_bridge.cpp
    receiver_engine_airplayserver.cpp
)

target_compile_features(localplay_android PRIVATE cxx_std_17)
target_include_directories(localplay_android PRIVATE airplayserver/lib)
target_link_options(
    localplay_android
    PRIVATE
    "-Wl,-z,max-page-size=16384"
    "-Wl,-z,common-page-size=16384"
)
target_link_libraries(localplay_android PRIVATE android log raopserver)
