# MultipleMatches Example

This example demonstrates how to run a large number of non-physics games concurrently in a single game server instance.

This would be most appropriate for Card, Board, Puzzle, and Arcade games where there is no physics involved, just presentation and messaging.

While this example is turn-based, real-time games work just as well.

When a match is started, a MatchController and Player objects are spawned, all with a Network Match Checker component with the same matchId set.  Only clients with the same matchID get messages about each others actions and about their own match.  They don't receive any data about other matches that may be running concurrently.
