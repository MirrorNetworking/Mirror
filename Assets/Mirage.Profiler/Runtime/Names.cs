namespace Mirage.NetworkProfiler
{
    public static class Names
    {
        public const string PLAYER_COUNT = "Player Count";
        public const string PLAYER_COUNT_TOOLTIP = "Number of players connected to the server";
        public const string CHARACTER_COUNT = "Character Count";
        public const string CHARACTER_COUNT_TOOLTIP = "Number of players with spawned GameObjects";

        public const string OBJECT_COUNT = "Object Count";
        public const string OBJECT_COUNT_TOOLTIP = "Number of NetworkIdentities spawned on the server";

        public const string SENT_COUNT = "Sent Messages";
        public const string SENT_BYTES = "Sent Bytes";
        public const string SENT_PER_SECOND = "Sent Per Second";

        public const string RECEIVED_COUNT = "Received Messages";
        public const string RECEIVED_BYTES = "Received Bytes";
        public const string RECEIVED_PER_SECOND = "Received Per Second";

        public const string PER_SECOND_TOOLTIP = "Sum of Bytes over the previous second";
    }
}
