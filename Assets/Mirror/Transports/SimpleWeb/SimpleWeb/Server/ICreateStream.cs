namespace Mirror.SimpleWeb
{
    public interface ICreateStream
    {
        bool TryCreateStream(IConnection conn);
    }
}
