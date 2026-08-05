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

#include "raop_server.h"
#include <string.h>
#include "log.h"
#include "logger.h"
#include "raop.h"
#include <malloc.h>
#include <math.h>
#include "threads.h"

static void *
audio_init(void *cls)
{
    audio_session_t *session;
    session = (audio_session_t *)calloc(1, sizeof(audio_session_t));
    session->volume = 1.0f;
    return session;
}
static void
audio_destroy(void *cls, void *opaque)
{
    audio_session_t *session = (audio_session_t *)opaque;
    free(session);
}
// 处理音量变化
static void
audio_set_volume(void *cls, void *opaque, float volume)
{
    audio_session_t *session = (audio_session_t *)opaque;
    session->volume = powf(10.0, 0.05 * volume);
}

//static void
//video_destroy(void *cls)
//{
//
//}

static void
log_callback(void *cls, int level, const char *msg) {
    switch (level) {
        case LOGGER_DEBUG: {
            LOGD("%s", msg);
            break;
        }
        case LOGGER_WARNING: {
            LOGW("%s", msg);
            break;
        }
        case LOGGER_INFO: {
            LOGI("%s", msg);
            break;
        }
        case LOGGER_ERR: {
            LOGE("%s", msg);
            break;
        }
        default:break;
    }
}

struct raop_server_s {
    raop_t* raop;
    int running;
    mutex_handle_t run_mutex;
};

raop_server_t*
raop_server_init(void *cls, audio_data_callback audio_callback, video_data_callback video_callback, video_destroy_callback destroy_callback)
{
    raop_server_t* raop_server;
    raop_server = (raop_server_t* ) calloc(1, sizeof(raop_server_t));
    if (!raop_server) {
        return NULL;
    }
    raop_callbacks_t raop_cbs;
    memset(&raop_cbs, 0, sizeof(raop_cbs));
    raop_cbs.cls = cls;
    raop_cbs.audio_init = audio_init;
    raop_cbs.audio_process = audio_callback;
    raop_cbs.audio_destroy = audio_destroy;
    raop_cbs.audio_set_volume = audio_set_volume;
    raop_cbs.video_process = video_callback;
    raop_cbs.video_destroy = destroy_callback;
    raop_server->raop = raop_init(1, &raop_cbs);
    if (raop_server->raop == NULL) {
        LOGE("raop = NULL");
        free(raop_server);
        return NULL;
    } else {
        LOGD("raop init success");
    }
    raop_set_log_callback(raop_server->raop, log_callback, NULL);
    raop_set_log_level(raop_server->raop, RAOP_LOG_DEBUG);
    raop_server->running = 0;
    MUTEX_CREATE(raop_server->run_mutex);
    return raop_server;
}

int
raop_server_start(raop_server_t* raop_server, const char* device_name, char* hw_addr, int hw_addr_len)
{
    MUTEX_LOCK(raop_server->run_mutex);
    if (raop_server->running) {
        MUTEX_UNLOCK(raop_server->run_mutex);
        return 0;
    }
    unsigned short port = 0;
    raop_set_hw_addr(raop_server->raop, hw_addr, hw_addr_len);
    raop_start(raop_server->raop, &port);
    raop_set_port(raop_server->raop, port);
    LOGD("raop port = %d; DNS-SD is registered by Android NsdManager", port);
    raop_server->running = 1;
    MUTEX_UNLOCK(raop_server->run_mutex);
    return RAOP_SERVER_NOERROR;
}

int
raop_server_is_running(raop_server_t *raop_server)
{
    int running;
    MUTEX_LOCK(raop_server->run_mutex);
    running = raop_server->running;
    MUTEX_UNLOCK(raop_server->run_mutex);
    return running;
}

int
raop_server_get_port(raop_server_t* raop_server)
{
    return raop_get_port(raop_server->raop);
}

void*
raop_server_get_cls(raop_server_t* raop_server)
{
    return raop_get_callback_cls(raop_server->raop);
}

void
raop_server_stop(raop_server_t* raop_server)
{
    MUTEX_LOCK(raop_server->run_mutex);
    if (!raop_server->running) {
        MUTEX_UNLOCK(raop_server->run_mutex);
        return;
    }
    raop_server->running = 0;
    raop_stop(raop_server->raop);
    MUTEX_UNLOCK(raop_server->run_mutex);
}

void
raop_server_destroy(raop_server_t* raop_server)
{
    raop_server_stop(raop_server);
    MUTEX_DESTROY(raop_server->run_mutex);
    raop_destroy(raop_server->raop);
    free(raop_server);
}
