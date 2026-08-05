cmake_minimum_required(VERSION 3.4.1)

set(FDK_AAC_LIB_PATH ${CMAKE_CURRENT_SOURCE_DIR}/../third_party/fdk-aac)

add_subdirectory(crypto)
add_subdirectory(curve25519)
add_subdirectory(ed25519)
add_subdirectory(playfair)
add_subdirectory(plist)
add_subdirectory(${FDK_AAC_LIB_PATH} fdk_out)

include_directories(crypto curve25519 ed25519 playfair plist/plist
        ${FDK_AAC_LIB_PATH}/libAACdec/include
        ${FDK_AAC_LIB_PATH}/libAACenc/include
        ${FDK_AAC_LIB_PATH}/libFDK/include
        ${FDK_AAC_LIB_PATH}/libMpegTPDec/include
        ${FDK_AAC_LIB_PATH}/libMpegTPEnc/include
        ${FDK_AAC_LIB_PATH}/libPCMutils/include
        ${FDK_AAC_LIB_PATH}/libSBRdec/include
        ${FDK_AAC_LIB_PATH}/libSBRenc/include
        ${FDK_AAC_LIB_PATH}/libSYS/include
        #${FDK_AAC_LIB_PATH}/libArithCoding/include
        #${FDK_AAC_LIB_PATH}/libDRCdec/include
        #${FDK_AAC_LIB_PATH}/libSACdec/include
        #${FDK_AAC_LIB_PATH}/libSACenc/include
        )

aux_source_directory(. play_src)
set(DIR_SRCS ${play_src})
list(FILTER DIR_SRCS EXCLUDE REGEX ".*/dnssd\\.c$")

add_library(raopserver STATIC ${DIR_SRCS})

if(ANDROID)
    find_library( log-lib
            log )
    target_link_libraries( raopserver
            crypto
            curve25519
            ed25519
            playfair
            plist
            fdk-aac
            ${log-lib})
else()
    find_library(WS2_32_LIBRARY ws2_32)
    target_link_libraries( raopserver
            crypto
            curve25519
            ed25519
            playfair
            plist
            fdk-aac
            ws2_32)
endif()


target_include_directories(raopserver
        PUBLIC
        $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}>)
