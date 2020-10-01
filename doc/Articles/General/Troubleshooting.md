# Troubleshooting

## No Writer found for X

MirrorNG normally generates readers and writers for any [Data Type](../Guides/DataTypes.md)
In order to do so,  it needs to know what types you want to read or write.
You are getting this error because MirrorNG did not know you wanted to read or write this type.

MirrorNG scans your code looking for calls to Send, ReceiveHandler, Write or Read. It will also recognize syncvars and parameters to rpc methods. If it does not find one,  it assumes you are not trying to serialize the type so it does not generate the reader and writer.

For example, you might get this error with this code when trying to sync the synclist.

```cs
struct MyCustomType {
    public int id;
    public string name;
}

class MyBehaviour : NetworkBehaviour {
    SyncList<MyCustomType> mylist = new SyncList<MyCustomType>();
}
```

In this case there is no direct invocation to send or receive.  So MirrorNG does not know about it. 

There is a simple workaround: add a call to `NetworkWriter.Write<MyCustomType>` anywhere in your code. For example:

```cs
struct MyCustomType {
    public int id;
    public string name;
}

class MyBehaviour : NetworkBehaviour {
    SyncList<MyCustomType> mylist = new SyncList<MyCustomType>();

    // This is a dummy method that is never called.
    // When MirrorNG builds your app,  it will look at this code
    // and realize that you do want to read and write MyCustomType
    // so it will generate the reader and writer for it
    void DummyWrite(NetworkWriter writer) {
        writer.Write(new MyCustomType());
    }
}
```