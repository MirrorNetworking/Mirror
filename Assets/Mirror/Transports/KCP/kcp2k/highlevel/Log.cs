// A simple logger class that uses Console.WriteLine by default.
// Can also do Logger.LogMethod = Debug.Log for Godot etc.
// (this way we don't have to depend on GodotEngine)
using System;

namespace kcp2k
{
    public static class Log
    {
        public static Action<string> Info    = Console.WriteLine;
        public static Action<string> Warning = Console.WriteLine;
        public static Action<string> Error   = Console.Error.WriteLine;
    }
}
