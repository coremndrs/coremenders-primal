using Newtonsoft.Json;

namespace Game.Persistence
{
    public sealed class NewtonsoftSerializer : ISaveSerializer
    {
        public static readonly NewtonsoftSerializer Instance = new NewtonsoftSerializer();

        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented
        };

        public string Serialize<T>(T obj)   => JsonConvert.SerializeObject(obj, Settings);
        public T Deserialize<T>(string json) => JsonConvert.DeserializeObject<T>(json);
    }
}
