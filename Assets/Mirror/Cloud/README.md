# Mirror Cloud Services

## Mirror List Server

Example has API key that can be used for as a demo.

To get an API key to use within your game you can subscribe on the [Mirror Networking Website](https://mirror-networking.com/list-server/)

### Key features

- The Cloud Service works via https so is secure and can be used from any platform. 
- It runs on Google Cloud so there is no worry about server down time.
- It scales really well. Default quota is 1000 API requests per minute. If you have high demands, contact us and we can increase that limit. 

## List Server Examples

An example for this can be found in Mirror/Examples/Cloud/

*Note: you can not connect to your own public ip address, you will need at least people to test this*

## How to use

Add `ApiConnector` component to an object in your game, It is probably best to put this on the same object as your NetworkManager. Once it has been added set the `ApiAddress` and `ApiKey` fields.

To use `ApiConnector` either directly reference it in an inspector field or get it when your script awakes
```cs
ApiConnector connector;

void Awake()
{
    connector = FindObjectOfType<ApiConnector>();
}
```


The Api calls are grouped into objects. `connector.ListServer.ServerApi` has the Server api calls like `AddServer`. `connector.ListServer.ClientApi` has the Client Api calls like `GetServerList`.

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
