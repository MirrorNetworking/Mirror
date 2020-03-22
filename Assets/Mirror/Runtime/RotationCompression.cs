namespace Mirror
{
    /// <summary>
    /// Compresses 16 Byte Quaternion into None=12, Much=3, Lots=2 Byte
    /// </summary>
    public enum RotationCompression
    {
        None,
        Much,
        Lots,
        NoRotation
    };
}
