let webSocket = undefined;

function IsConnected() {
    if (webSocket) {
        return webSocket.readyState === webSocket.OPEN;
    }
    else {
        return false;
    }
}

function Connect(addressPtr, openCallbackPtr, closeCallBackPtr, messageCallbackPtr, errorCallbackPtr) {
    const address = Pointer_stringify(addressPtr);
    console.log("Connecting to " + address);
    // Create webSocket connection.
    webSocket = new WebSocket(address);
    webSocket.binaryType = 'arraybuffer';

    // Connection opened
    webSocket.addEventListener('open', function (event) {
        console.log("Connected to " + address);
        Runtime.dynCall('v', openCallbackPtr, 0);
    });
    webSocket.addEventListener('close', function (event) {
        console.log("Disconnected from " + address);
        Runtime.dynCall('v', closeCallBackPtr, 0);
    });

    // Listen for messages
    webSocket.addEventListener('message', function (event) {
        if (event.data instanceof ArrayBuffer) {
            // TODO dont alloc each time
            var array = new Uint8Array(event.data);
            var arrayLength = array.length;

            var bufferPtr = _malloc(arrayLength);
            var dataBuffer = new Uint8Array(HEAPU8.buffer, bufferPtr, arrayLength);
            dataBuffer.set(array);

            Runtime.dynCall('vii', messageCallbackPtr, [bufferPtr, arrayLength]);
            _free(bufferPtr);
        }
        else {
            console.error("message type not supported")
        }
    });

    webSocket.addEventListener('error', function (event) {
        console.error('Socket Error', event);

        Runtime.dynCall('v', errorCallbackPtr, 0);
    });
}

function Disconnect() {
    if (webSocket) {
        webSocket.close(1000, "Disconnect Called by Mirror");
    }

    webSocket = undefined;
}

function Send(arrayPtr, offset, length) {
    if (webSocket) {
        const start = arrayPtr + offset;
        const end = start + length;
        const data = HEAPU8.buffer.slice(start, end);
        webSocket.send(data);
    }
}

mergeInto(LibraryManager.library, {
    IsConnected,
    Connect,
    Disconnect,
    Send
});