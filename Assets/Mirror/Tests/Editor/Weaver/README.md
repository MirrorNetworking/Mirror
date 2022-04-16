There are two types of Weaver tests:
* Success tests where we simply have to guarantee that a class is 
  weaved without issues.
* Failure tests where we need to make sure certain classes are not
  weaved because they aren't allowed to.

The success tests can be regular C# files.
=> Weaver runs automatically when creating them, so we don't even
   need to weave those manually with AssemblyBuilder.
=> There are >100 of those tests. moving them to regular C# 
   removes a LOT of AssemblyBuilder time.

The failure tests need to be weaved one at a time.
=> Weaver usually stops weaving after the first error.
=> So we weave them all separately to get all the errors.
