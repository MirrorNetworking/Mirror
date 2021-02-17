# Network Owner Checker

The Network Owner Checker component controls visibility of networked objects based on owner client.

Any object with this component on it will only be visible to the client that has authority ownership of it.

This can be used when you want to spawn a network object with client authority and only have it appear for the owner, e.g. an interactive UI or NPC.
Since clients can call commands on objects they have authority over, such objects can have Network Behaviors with commands in them that can be called directly by the client.
