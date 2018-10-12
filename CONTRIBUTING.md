# How to contribute

We are really glad you're reading this, because we need volunteer developers to help this project improve.

If you haven't already, come find us in [Discord](https://discord.gg/wvesC6). We want you working on things you're excited about, and we can give you instant feedback.

## Testing

We have a handful of unit tests, but most of our testbed consists of running it with existing projects. 
Try our builds and pull requests in your own projects and let us know how it goes.

## Submitting changes

Please send a [GitHub Pull Request](https://github.com/vis2k/HLAPI-Community-Edition/pull/new/improvements) with a clear list of what you've done (read more about [pull requests](http://help.github.com/pull-requests/)). 
When you send a pull request, we will love you forever if you include unit tests. 
We can always use more test coverage. 
Please follow our coding conventions (below) and make sure all of your commits are atomic (one feature per commit). Rebase your pull requests if necessary.

Always write a clear log message for your commits. One-line messages are fine for small changes, but bigger changes should look like this:

    $ git commit -m "A brief summary of the commit
    > 
    > A paragraph describing what changed and its impact."
    
Submit your pull requests to the right branch:
* fixes for bug fixes 
* improvements for refactorings and cleanups,  we love to delete code.
* features for new shiny features you want in HLAPI.
  
If your pull request breaks any test,  it has no hope of being merged.

## Coding conventions

Start reading our code and you'll get the hang of it. We optimize for readability:

* We indent using 4 spaces (soft tabs)
* We value simplicity. The code should be easy to read and avoid magic
* We use default visual studio code formatting standard
* This is open source software. Consider the people who will read your code, and make it look nice for them. It's sort of like driving a car: Perhaps you love doing donuts when you're alone, but with passengers the goal is to make the ride as smooth as possible.
 
Thanks.
