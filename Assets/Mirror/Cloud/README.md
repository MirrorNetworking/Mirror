# Mirror Cloud Services

## Mirror List Server

Example has an API key which can be used as a demo.

To get an API key to use within your game you can subscribe on the [Mirror Networking Website](https://mirror-networking.com/list-server/)

### Key features

- The Cloud Service works via https so it is secure and can be used from any platform. 
- It runs on Google Cloud so there is no worry about server downtime.
- It scales really well. Default quota is 1000 API requests per minute. If you have high demands, contact us and we can increase that limit. 

## List Server Examples

An example for this can be found in Mirror/Examples/Cloud/

*Note: you cannot connect to your own public IP address, you will need at least one other person to test this*

## How to use

Add `ApiConnector` component to an object in your game. It is probably best to put this on the same object as your NetworkManager. Once it has been added set the `ApiAddress` and `ApiKey` fields.

To use `ApiConnector` either directly reference it in an inspector field or get it when your script awakes
```cs
ApiConnector connector;

void Awake()
{
    connector = FindObjectOfType<ApiConnector>();
}
```


The Api calls are grouped into objects. `connector.ListServer.ServerApi` has the Server Api calls like `AddServer`. `connector.ListServer.ClientApi` has the Client Api calls like `GetServerList`.

### Server Api Example

Example of how to add server
```cs
void AddServer(int playerCount)
{
    Transport transport = Transport.activeTransport;

    Uri uri = transport.ServerUri();
    int port = uri.Port;
    string protocol = uri.Scheme;

    connector.ListServer.ServerApi.AddServer(new ServerJson
    {
        displayName = "Fun game!!!",
        protocol = protocol,
        port = port,
        maxPlayerCount = NetworkManager.singleton.maxConnections,
        playerCount = playerCount
    });
}
```

### Client Api Example
Example of how to list servers 

```cs
ApiConnector connector;

void Awake()
{
    connector = FindObjectOfType<ApiConnector>();
    // add listener to event that will update UI when Server list is refreshed
    connector.ListServer.ClientApi.onServerListUpdated += onServerListUpdated;

    // add listen to button so that player can refresh server list
    refreshButton.onClick.AddListener(RefreshButtonHandler);
}

public void RefreshButtonHandler()
{
    connector.ListServer.ClientApi.GetServerList();
}

void onServerListUpdated() 
{
    // Update UI here
}
```


## Debug

If something doesn't seem to be working then here are some tips to help solve the problem

### Check logs

Enable `showDebugMessages` on your NetworkManager or use the log level window to enable logging for the cloud scripts

Below are some example logs to look for to check things are working.

#### Add Server

The add request is sent to add a server to the list server

```
Request: POST servers {"protocol":"kcp","port":7777,"playerCount":0,"maxPlayerCount":4,"displayName":"Tanks Game 521","address":"","customAddress":"","customData":[]}
```
```
Response: POST 200 /servers {"id":"BI6bQQ2TbNiqhdp1D7UB"}
```

#### Update Server

The object sent in update request maybe be empty, this is sent to keep the server record alive so it shows up.

The update request can also be used to change info. For example the player count when someone joins or leaves

```
Request: PATCH servers/BI6bQQ2TbNiqhdp1D7UB {}
```
```
Response: PATCH 204 /servers/BI6bQQ2TbNiqhdp1D7UB
```

#### Remove Server

The remove request is sent to remove a server from the list server. This is automatically called when the ApiConnection is destroyed.

```
Request: DELETE servers/BI6bQQ2TbNiqhdp1D7UB
```
```
Response: DELETE 204 /servers/BI6bQQ2TbNiqhdp1D7UB 
```


#### Get Servers

The get request is sent in order to get the list of servers.

The example below shows an array of 2 servers, one with name `Tanks Game 521` and the other with name `Tanks Game 212`

```
Request: GET servers
```
```
Response: GET 200 /servers {"servers":[{"address":"kcp://xx.xx.xx.xx:7777","displayName":"Tanks Game 521","port":7777,"protocol":"kcp","playerCount":0,"maxPlayerCount":4,"customAddress":"","customData":[]},{"address":"kcp://xx.xx.xx.xx:7777","displayName":"Tanks Game 212","port":7777,"protocol":"kcp","playerCount":0,"maxPlayerCount":4,"customData":[]}]}
```
*xx.xx.xx.xx will be the IP address for the server*


### Use the QuickListServerDebug

The QuickListServerDebug script uses `OnGUI` to show the list of servers. This script can be used to check the server list without using Canvas UI.
