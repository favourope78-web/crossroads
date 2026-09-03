using UnityEngine;

namespace Crossroads.Core
{
    /// <summary>JsonUtility-backed serializer (design §12.1: JSON via Unity JsonUtility + wrapper).</summary>
    public class UnityJsonSerializer : IJsonSerializer
    {
        public string ToJson(object o, bool prettyPrint)
        {
            return JsonUtility.ToJson(o, prettyPrint);
        }

        public T FromJson<T>(string json)
        {
            return JsonUtility.FromJson<T>(json);
        }
    }
}
