namespace Mirror.CloudServices.ListServerService
{
    public abstract class ListServerBaseApi : BaseApi
    {
        protected ListServerBaseApi(ICoroutineRunner runner, IRequestCreator requestCreator) : base(runner, requestCreator)
        {
        }
    }
}
