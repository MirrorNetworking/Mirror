var LibraryWebSockets = {
	$webSocketInstances: [],

	SocketCreate: function(url, id, onopen, ondata, onclose)
	{
		var str = Pointer_stringify(url);

		var socket = new WebSocket(str, "binary");

		socket.binaryType = 'arraybuffer';

		socket.onopen = function(e) {
			Runtime.dynCall('vi', onopen, [id]);
		}

		socket.onerror = function(e) {
			console.log("websocket error " + JSON.stringify(e));
		}

		socket.onmessage = function (e) {
			// Todo: handle other data types?
			if (e.data instanceof Blob)
			{
				var reader = new FileReader();
				reader.addEventListener("loadend", function() {
					var array = new Uint8Array(reader.result);
				});
				reader.readAsArrayBuffer(e.data);
			}
			else if (e.data instanceof ArrayBuffer)
			{
				var array = new Uint8Array(e.data);
				var ptr = _malloc(array.length);
				var dataHeap = new Uint8Array(HEAPU8.buffer, ptr, array.length);
				dataHeap.set(array);
				Runtime.dynCall('viii', ondata, [id, ptr, array.length]);
				_free(ptr);
			}
			else if(typeof e.data === "string") {
				var reader = new FileReader();
				reader.addEventListener("loadend", function() {
					var array = new Uint8Array(reader.result);
				});
				var blob = new Blob([e.data]);
				reader.readAsArrayBuffer(blob);
			}
		};

		socket.onclose = function (e) {
			Runtime.dynCall('vi', onclose, [id]);

			if (e.code != 1000)
			{
				if (e.reason != null && e.reason.length > 0)
					socket.error = e.reason;
				else
				{
					switch (e.code)
					{
						case 1001: 
							socket.error = "Endpoint going away.";
							break;
						case 1002: 
							socket.error = "Protocol error.";
							break;
						case 1003: 
							socket.error = "Unsupported message.";
							break;
						case 1005: 
							socket.error = "No status.";
							break;
						case 1006: 
							socket.error = "Abnormal disconnection.";
							break;
						case 1009: 
							socket.error = "Data frame too large.";
							break;
						default:
							socket.error = "Error "+e.code;
					}
				}
			}
		}
		var instance = webSocketInstances.push(socket) - 1;
		return instance;
	},

	SocketState: function (socketInstance)
	{
		var socket = webSocketInstances[socketInstance];
		return socket.readyState;
	},

	SocketSend: function (socketInstance, ptr, length)
	{
		var socket = webSocketInstances[socketInstance];
		socket.send (HEAPU8.buffer.slice(ptr, ptr+length));
	},

	SocketClose: function (socketInstance)
	{
		var socket = webSocketInstances[socketInstance];
		socket.close();
	}
};

autoAddDeps(LibraryWebSockets, '$webSocketInstances');
mergeInto(LibraryManager.library, LibraryWebSockets);