# Changing playerController to identity

If you need to fix up a project after `NetworkConnection.playerController` was changed to `identity` these instructions should be helpful.

1. Open your Unity project and look for Assets/Mirror/Runtime/**NetworkConnection**:

![Project window in Unity](pc2i-1.png)
 

2. Open this file in Visual Studio or Visual Code from Unity and look for these lines:

![Code snip in NetworkConnection.cs](pc2i-2.png)

The line numbers could be off a bit if minor file changes happen above them after this document was written.
 

3. Comment the last line shown:

```cs
    // public NetworkIdentity identity { get; internal set; }
```
 

4. Double-click and then right-click `playerController` and select Rename:

![Start of Rename process](pc2i-3.png)
 

5. Change `playerController` to `identity` and click Apply:

![Finishing the Rename process](pc2i-4.png)
 

Visual Studio will now have applied the change throughout your project, but you're not done yet!

Without using the replace feature this time, simply retype the name back to `playerController` and un-comment the last line in the code image that you commented out in step 3.

Your code should now look like the code image again.

![Code snip in NetworkConnection.cs](pc2i-2.png)
 
 
**Save your work!**
