using System;

namespace Mirror.Cloud
{
    public interface IBaseApi
    {
        /// <summary>
        /// Cleans up any data created by the instance
        /// <para>For Example: removing server from list</para>
        /// </summary>
        void Shutdown();
    }

    public abstract class BaseApi
    {
        protected readonly ICoroutineRunner runner;
        protected readonly IRequestCreator requestCreator;

        protected BaseApi(ICoroutineRunner runner, IRequestCreator requestCreator)
        {
            this.runner = runner ?? throw new ArgumentNullException(nameof(runner));
            this.requestCreator = requestCreator ?? throw new ArgumentNullException(nameof(requestCreator));
        }
    }
}
