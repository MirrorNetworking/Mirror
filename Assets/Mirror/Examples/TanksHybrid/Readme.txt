Tanks demo, running on Mirror's new Hybrid sync.
In other words, Unreliable sync for NetworkTransform etc.

Note that while Mirror now has hybrid sync baked into the core,
we didn't adapt the Weaver yet. Which means that [SyncVar]s don't work with Hybrid sync yet.
Still need to use OnSerialize/OnDeserialize manually for now.