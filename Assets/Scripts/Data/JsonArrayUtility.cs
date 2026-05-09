using UnityEngine;

namespace ProphecyCentury.Data
{
    public static class JsonArrayUtility
    {
        public static T[] FromJsonArray<T>(string json)
        {
            var wrapped = "{\"items\":" + json + "}";
            var container = JsonUtility.FromJson<ArrayWrapper<T>>(wrapped);
            return container?.items ?? new T[0];
        }

        [System.Serializable]
        private sealed class ArrayWrapper<T>
        {
            public T[] items;
        }
    }
}
