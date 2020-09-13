# FAQ

> work in progress

<details>
  <summary>How to Send/Sync custom data types?</summary>
  
  Mirror can automatically create Serialization functions for many custom data types when your scripts are compiled.

  For example, mirror will automatically create a functions for `MyCustomStruct` so that it can be sent without any extra work.
  ```cs
  [ClientRpc]
  public void RpcDoSomething(MyCustomStruct data)
  {
      // do stuff here
  }

  struct MyCustomStruct
  {
      int someNumber;
      Vector3 somePosition;
  }
  ```

  For More details 
  - [DataTypes](https://mirror-networking.com/docs/Guides/DataTypes.html)
  - [Serialization](https://mirror-networking.com/docs/Guides/Serialization.html)
</details>
