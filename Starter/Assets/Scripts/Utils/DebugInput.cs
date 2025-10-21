#nullable enable

using UnityEngine;

namespace Nex.Dev
{
    public static class DebugInput
    {
        public static bool GetKey(KeyCode key)
        {
#if PRODUCTION
            return false;
#else
            return Input.GetKey(key);
#endif
        }

        public static bool GetKeyDown(KeyCode key)
        {
#if PRODUCTION
            return false;
#else
            return Input.GetKeyDown(key);
#endif
        }

        public static bool GetKeyUp(KeyCode key)
        {
#if PRODUCTION
            return false;
#else
            return Input.GetKeyUp(key);
#endif
        }
    }
}
