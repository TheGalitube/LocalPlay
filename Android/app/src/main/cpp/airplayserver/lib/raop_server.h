/*
 * MIT License
 *
 * Copyright (c) 2019 dsafa22, All Rights Reserved.
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

/**
 * 整合了raop和dnssd
 * dnssd依赖iTunes，使用dnssd之前需要安装iTunes
 */

#ifndef AIRPLAYSERVER_RAOP_SERVER_H
#define AIRPLAYSERVER_RAOP_SERVER_H

#include "stream.h"

#if defined (_WIN32)
# define RAOP_SERVER_API __declspec(dllexport)
#else
# define RAOP_SERVER_API
#endif

#ifdef __cplusplus
extern "C" {
#endif

#define RAOP_SERVER_NOERROR                 0
#define RAOP_SERVER_ERROR_DNSSD_INIT        1001
/* MDNS 错误，只区分了服务未运行和其他错误，用作提示 */
#define RAOP_SERVER_ERROR_DNSSD_NOSERVICE   1002
#define RAOP_SERVER_ERROR_DNSSD_OTHER       1003

typedef struct {
    float volume;
} audio_session_t;

typedef struct raop_server_s raop_server_t;

typedef void (*audio_data_callback)(void *cls, void *session, pcm_data_struct *data);
typedef void (*video_data_callback)(void *cls, h264_decode_struct *data);
typedef void (*video_destroy_callback)(void *cls);

raop_server_t *
raop_server_init(void *cls, audio_data_callback audio_callback, video_data_callback video_callback, video_destroy_callback destroy_callback);
int raop_server_start(raop_server_t *raop_server, const char *device_name, char *hw_addr,
                      int hw_addr_len);
int raop_server_get_port(raop_server_t *raop_server);
void *raop_server_get_cls(raop_server_t *raop_server);
void raop_server_stop(raop_server_t *raop_server);
void raop_server_destroy(raop_server_t *raop_server);
int raop_server_is_running(raop_server_t *raop_server);


#ifdef __cplusplus
}
#endif

#endif //AIRPLAYSERVER_RAOP_SERVER_H
