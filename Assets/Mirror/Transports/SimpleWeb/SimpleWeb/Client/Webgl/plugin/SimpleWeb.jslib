// this will create a global object
const SimpleWeb =
{
    webSockets: [],
    next: 1,
    GetWebSocket: function (index)
    {
        return SimpleWeb.webSockets[index]
    },
    AddNextSocket: function (webSocket)
    {
        var index = SimpleWeb.next;
        SimpleWeb.next++;
        SimpleWeb.webSockets[index] = webSocket;
        return index;
    },
    RemoveSocket: function (index)
    {
        SimpleWeb.webSockets[index] = undefined;
    },
};

function IsConnected(index)
{
    var webSocket = SimpleWeb.GetWebSocket(index);
    if (webSocket)
        return webSocket.readyState === webSocket.OPEN;
    else
        return false;
}

function Connect(addressPtr, openCallbackPtr, closeCallBackPtr, messageCallbackPtr, errorCallbackPtr)
{
    const address = UTF8ToString(addressPtr);
    console.log("Connecting to " + address);

    // Create webSocket connection.
    var webSocket = new WebSocket(address);
    webSocket.binaryType = 'arraybuffer';

    const index = SimpleWeb.AddNextSocket(webSocket);

    // Connection opened
    webSocket.onopen = function(event) 
    {
        console.log("Connected to " + address);
        // dynCall('vi', openCallbackPtr, [index]);
        {{{ makeDynCall('vi', 'openCallbackPtr') }}}(index);
    };

    webSocket.onclose = function(event) 
    {
        console.log("Disconnected from " + address);
        // dynCall('vi', closeCallBackPtr, [index]);
        {{{ makeDynCall('vi', 'closeCallBackPtr') }}}(index);
    };

    webSocket.onmessage = function(event) 
    {
        if (event.data instanceof ArrayBuffer) {
            var array = new Uint8Array(event.data);
            var arrayLength = array.length;

            var bufferPtr = _malloc(arrayLength);
            var dataBuffer = new Uint8Array(HEAPU8.buffer, bufferPtr, arrayLength);
            dataBuffer.set(array);

            // dynCall('viii', messageCallbackPtr, [index, bufferPtr, arrayLength]);
            {{{ makeDynCall('viii', 'messageCallbackPtr') }}}(index, bufferPtr, arrayLength);
            _free(bufferPtr);
        }
        else
        {
            console.error("message type not supported")
        }
    };

    webSocket.onerror = function(event) 
    {
        console.error('Socket Error', event);
        // dynCall('vi', errorCallbackPtr, [index]);
        {{{ makeDynCall('vi', 'errorCallbackPtr') }}}(index);
    };

    return index;
}

function Disconnect(index) {
    var webSocket = SimpleWeb.GetWebSocket(index);
    if (webSocket)
        webSocket.close(1000, "Disconnect Called by Mirror");

    SimpleWeb.RemoveSocket(index);
}

function Send(index, arrayPtr, offset, length) {
    var webSocket = SimpleWeb.GetWebSocket(index);
    if (webSocket)
    {
        const start = arrayPtr + offset;
        const end = start + length;
        const data = HEAPU8.buffer.slice(start, end);
        webSocket.send(data);
        return true;
    }
    return false;
}

const SimpleWebLib =
{
    $SimpleWeb: SimpleWeb,
    IsConnected,
    Connect,
    Disconnect,
    Send
};

autoAddDeps(SimpleWebLib, '$SimpleWeb');
mergeInto(LibraryManager.library, SimpleWebLib);
