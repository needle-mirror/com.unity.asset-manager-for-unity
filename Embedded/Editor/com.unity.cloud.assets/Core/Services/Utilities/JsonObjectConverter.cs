using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Unity.Cloud.AssetsEmbedded
{
    /// <summary>
    /// JsonObjectConverter overrides behaviour from JsonConverter to allow
    /// encapsulation of raw object types and conversion between different
    /// types.
    /// </summary>
    class JsonObjectConverter : JsonConverter
    {
        /// <inheritdoc/>
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if(value is IDeserializable deserializable and not JsonObject)
            {
                JObject.Parse(deserializable.GetAsString()).WriteTo(writer);
                return;
            }

            var jobj = (JsonObject) value;

            if (jobj?.obj == null)
            {
                writer.WriteNull();
                return;
            }

            var t = JToken.FromObject(jobj.obj, serializer);
            t.WriteTo(writer);
        }

        /// <inheritdoc/>
        public override object ReadJson(JsonReader reader, System.Type objectType, object existingValue, JsonSerializer serializer)
        {
            switch (reader.TokenType)
            {
                case JsonToken.Null:
                    return new JsonObject(null);
                case JsonToken.StartObject:
                case JsonToken.EndObject:
                    var jsonObject = JObject.Load(reader);
                    return new JsonObject(jsonObject);
                case JsonToken.StartArray:
                case JsonToken.EndArray:
                    var jsonArray = JArray.Load(reader);
                    return new JsonObject(jsonArray);
                default:
                    return new JsonObject(reader.Value);
            }
        }

        /// <inheritdoc/>
        public override bool CanConvert(System.Type objectType)
        {
            return typeof(IDeserializable).IsAssignableFrom(objectType);
        }
    }
}
