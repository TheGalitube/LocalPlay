# Install script for directory: /Users/maxmittelstadt/Documents/GitHub/LocalPlay/Android/app/src/main/cpp/airplayserver/lib

# Set the install prefix
if(NOT DEFINED CMAKE_INSTALL_PREFIX)
  set(CMAKE_INSTALL_PREFIX "/usr/local")
endif()
string(REGEX REPLACE "/$" "" CMAKE_INSTALL_PREFIX "${CMAKE_INSTALL_PREFIX}")

# Set the install configuration name.
if(NOT DEFINED CMAKE_INSTALL_CONFIG_NAME)
  if(BUILD_TYPE)
    string(REGEX REPLACE "^[^A-Za-z0-9_]+" ""
           CMAKE_INSTALL_CONFIG_NAME "${BUILD_TYPE}")
  else()
    set(CMAKE_INSTALL_CONFIG_NAME "Debug")
  endif()
  message(STATUS "Install configuration: \"${CMAKE_INSTALL_CONFIG_NAME}\"")
endif()

# Set the component getting installed.
if(NOT CMAKE_INSTALL_COMPONENT)
  if(COMPONENT)
    message(STATUS "Install component: \"${COMPONENT}\"")
    set(CMAKE_INSTALL_COMPONENT "${COMPONENT}")
  else()
    set(CMAKE_INSTALL_COMPONENT)
  endif()
endif()

# Install shared libraries without execute permission?
if(NOT DEFINED CMAKE_INSTALL_SO_NO_EXE)
  set(CMAKE_INSTALL_SO_NO_EXE "0")
endif()

# Is this installation the result of a crosscompile?
if(NOT DEFINED CMAKE_CROSSCOMPILING)
  set(CMAKE_CROSSCOMPILING "TRUE")
endif()

# Set default install directory permissions.
if(NOT DEFINED CMAKE_OBJDUMP)
  set(CMAKE_OBJDUMP "/Users/maxmittelstadt/Library/Android/sdk/ndk/27.3.13750724/toolchains/llvm/prebuilt/darwin-x86_64/bin/llvm-objdump")
endif()

if(NOT CMAKE_INSTALL_LOCAL_ONLY)
  # Include the install script for each subdirectory.
  include("/Users/maxmittelstadt/Documents/GitHub/LocalPlay/Android/app/.cxx/Debug/1e504d3m/arm64-v8a/airplayserver-build/crypto/cmake_install.cmake")
  include("/Users/maxmittelstadt/Documents/GitHub/LocalPlay/Android/app/.cxx/Debug/1e504d3m/arm64-v8a/airplayserver-build/curve25519/cmake_install.cmake")
  include("/Users/maxmittelstadt/Documents/GitHub/LocalPlay/Android/app/.cxx/Debug/1e504d3m/arm64-v8a/airplayserver-build/ed25519/cmake_install.cmake")
  include("/Users/maxmittelstadt/Documents/GitHub/LocalPlay/Android/app/.cxx/Debug/1e504d3m/arm64-v8a/airplayserver-build/playfair/cmake_install.cmake")
  include("/Users/maxmittelstadt/Documents/GitHub/LocalPlay/Android/app/.cxx/Debug/1e504d3m/arm64-v8a/airplayserver-build/plist/cmake_install.cmake")
  include("/Users/maxmittelstadt/Documents/GitHub/LocalPlay/Android/app/.cxx/Debug/1e504d3m/arm64-v8a/airplayserver-build/fdk_out/cmake_install.cmake")

endif()

