var WebSocketAdapter = {

    $State: {
        // map of int id -> WebSocket object
        websockets: {},
        
        // map of int id -> int returned from setTimeout
        timeoutIDs: {},

        // Callbacks to C# (common to all instances)
        onOpenCallback: null,
        onCloseCallback: null,
        onMessageCallback: null,
        onDataCallback: null,
        onErrorCallback: null,
    },

    // Set common callbacks
    JsWs_SetOnOpenCallback: function(obj) {
        State.onOpenCallback = obj;
    },
    JsWs_SetOnCloseCallback: function(obj) {
        State.onCloseCallback = obj;
    },
    JsWs_SetOnMessageCallback: function(obj) {
        State.onMessageCallback = obj;
    },
    JsWs_SetOnDataCallback: function (obj) {
        State.onDataCallback = obj;
    },
    JsWs_SetOnErrorCallback: function(obj) {
        State.onErrorCallback = obj;
    },

    // Returns WebSocket state:
    //   CONNECTING (0)
    //   OPEN (1)
    //   CLOSING (2)
    //   CLOSED (3)
    //   - from https://html.spec.whatwg.org/multipage/web-sockets.html#dom-websocket-readystate
    // or -1 if the connection has not been defined
    // 
    JsWs_GetState: function(id) {
        if (State.websockets && State.websockets[id]) {
            return State.websockets[id].readyState;
        }
        return -1;
    },

    JsWs_Connect: function(id, pUrl) {
        var url = UTF8ToString(pUrl);
        
        if (State.websockets[id])
        {
            try {
                State.websockets[id].close();
            } catch (Exception) {}

            delete State.websockets[id];
        }

        try {
            console.log('Connecting to - ' + url)
            ws = new WebSocket(url);
            ws.binaryType = "arraybuffer";
            State.websockets[id] = ws;
        } catch (e) {
            Module['dynCall_vii'](State.onErrorCallback, id, 1);
        }

        ws.onmessage = function (event) {
            if (typeof event.data === 'string') {
                var msg = event.data;
                var bufferSize = lengthBytesUTF8(msg) + 1;
                var buffer = _malloc(bufferSize);
                stringToUTF8(msg, buffer, bufferSize);
                try {
                    Module['dynCall_viii'](State.onMessageCallback, id, buffer, bufferSize);
                } finally {
                    _free(buffer);
                }
            }
            else if (event.data instanceof ArrayBuffer) {
                var dataBuffer = new Uint8Array(event.data);
                var bufferSize = dataBuffer.length;
                var buffer = _malloc(bufferSize);
                HEAPU8.set(dataBuffer, buffer);
                try {
                    Module['dynCall_viii'](State.onDataCallback, id, buffer, bufferSize);
                } finally {
                    _free(buffer);
                }
            }
        };

        ws.onopen = function(event) {
            console.log('callback onopen')
            Module['dynCall_vi'](State.onOpenCallback, id);
        };

        ws.onclose = function(event) {
            console.log('callback onclose');
            Module['dynCall_vi'](State.onCloseCallback, id);
        };

        ws.onerror = function(event) {
            console.log('callback onerror: type='+event.type);
            Module['dynCall_vii'](State.onErrorCallback, id, 0);
        }
    },

    JsWs_Close: function(id) {
        if (State.timeoutIDs[id]) {
            clearTimeout(State.timeoutIDs[id]);
            delete State.timeoutIDs[id];
        }

        var instance = State.websockets[id];
        instance.close();
        delete State.websockets[id];
    },

    JsWs_Send_Message: function(id, pMsg) {
        var msg = UTF8ToString(pMsg);
        var ws = State.websockets[id];
        if (msg && ws) {
            try {
                ws.send(msg);
            } catch (e) {
                Module['dynCall_vii'](State.onErrorCallback, id, 2);
            }
        }
    },

    JsWs_Send_Data: function (id, bufferPtr, offset, count) {
        var ws = State.websockets[id];
        if (ws) {
            try {
                ws.send(HEAPU8.buffer.slice(bufferPtr + offset, bufferPtr + count - offset));
            } catch (e) {
                Module['dynCall_vii'](State.onErrorCallback, id, 2);
            }
        }
    },

};

autoAddDeps(WebSocketAdapter, '$State');
mergeInto(LibraryManager.library, WebSocketAdapter);