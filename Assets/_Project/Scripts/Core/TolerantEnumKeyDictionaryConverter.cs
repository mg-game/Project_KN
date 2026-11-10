using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace IdleGame
{
    /// <summary>
    /// 열거형 키 딕셔너리(Dictionary&lt;TEnum, TValue&gt;)를 읽을 때 이 빌드가 모르는 키는 건너뛴다.
    /// Newtonsoft는 열거형 키를 숫자가 아닌 이름("Gold", "GuardianKillCount")으로 저장하므로,
    /// 열거형 멤버 이름이 바뀌거나 빠진 빌드(롤백/구버전)가 그 세이브를 읽으면 기본 동작으로는 키 하나 때문에
    /// 세이브 전체가 예외로 실패한다. 쓰기는 기본 직렬화를 그대로 쓴다.
    /// </summary>
    public sealed class TolerantEnumKeyDictionaryConverter : JsonConverter
    {
        public override bool CanWrite => false;

        public override bool CanConvert(Type objectType) => TryGetKeyValueTypes(objectType, out _, out _);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;
            if (reader.TokenType != JsonToken.StartObject)
                throw new JsonSerializationException($"딕셔너리 자리에 {reader.TokenType} 값이 있습니다. ({reader.Path})");

            TryGetKeyValueTypes(objectType, out var keyType, out var valueType);
            // 기본 동작과 같게 필드 초기값으로 만들어진 딕셔너리가 있으면 거기에 채운다
            var dictionary = existingValue as IDictionary ?? (IDictionary)Activator.CreateInstance(objectType);

            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonToken.Comment:
                        break;
                    case JsonToken.EndObject:
                        return dictionary;
                    case JsonToken.PropertyName:
                        string name = (string)reader.Value;
                        if (!reader.Read()) throw new JsonSerializationException($"딕셔너리 값이 없습니다. ({reader.Path})");
                        if (TryParseKey(keyType, name, out var key))
                        {
                            dictionary[key] = serializer.Deserialize(reader, valueType);
                        }
                        else
                        {
                            Debug.LogWarning($"[Save] 알 수 없는 {keyType.Name} 키 '{name}'를 건너뜁니다. ({reader.Path})");
                            reader.Skip();
                        }
                        break;
                    default:
                        throw new JsonSerializationException($"딕셔너리에 예상하지 못한 {reader.TokenType} 토큰이 있습니다. ({reader.Path})");
                }
            }
            throw new JsonSerializationException("딕셔너리가 끝나지 않았습니다.");
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) =>
            throw new NotSupportedException("쓰기는 기본 직렬화를 사용합니다.");

        static bool TryGetKeyValueTypes(Type objectType, out Type keyType, out Type valueType)
        {
            keyType = valueType = null;
            if (!objectType.IsGenericType || objectType.GetGenericTypeDefinition() != typeof(Dictionary<,>)) return false;

            var args = objectType.GetGenericArguments();
            if (!args[0].IsEnum) return false;
            keyType = args[0];
            valueType = args[1];
            return true;
        }

        /// <summary>이름("Gold") 또는 숫자 문자열("3")을 열거형 값으로 바꾼다. 숫자는 정의되지 않은 값이어도 기본 동작처럼 그대로 살린다</summary>
        static bool TryParseKey(Type keyType, string name, out object key)
        {
            key = null;
            if (string.IsNullOrWhiteSpace(name)) return false;
            try
            {
                key = Enum.Parse(keyType, name, true);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (OverflowException)
            {
                return false;
            }
        }
    }
}
