#nullable enable

using System;
using Unity.Services.RemoteConfig;

namespace Nex
{
    [Serializable]
    public class RemoteConfig
    {
        public string exampleConfig = "";

        public static RemoteConfig Create(RuntimeConfig appConfig, RemoteConfig defaultValues)
        {
            return new RemoteConfig
            {
                exampleConfig = appConfig.GetString(nameof(exampleConfig), defaultValues.exampleConfig)
            };
        }
    }
}
