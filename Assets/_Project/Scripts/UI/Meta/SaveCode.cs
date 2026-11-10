using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using Newtonsoft.Json;

namespace IdleGame.UI
{
    /// <summary>
    /// 세이브 코드 (SAVE-01, 서버 없이 기기를 옮기는 수단). 세이브 JSON → gzip → Base64 + CRC32.
    /// 형식: "LL1.{crc32 16진 8자}.{base64}". 내보내기(Export) · 검사(TryDecode) · 가져오기 읽기(TryParse)를 한다.
    /// 읽기는 예외를 내지 않는다 (실패 이유는 SaveCodeError). 풀어 낸 JSON은 4 MB까지만 읽는다.
    /// </summary>
    public static class SaveCode
    {
        const string Prefix = "LL1.";
        /// <summary>체크섬 16진 글자 수 (Export가 늘 8자로 쓴다)</summary>
        const int CrcHexLength = 8;

        /// <summary>풀어 낸 세이브 JSON의 최대 크기 (바이트). 넘으면 TooLarge</summary>
        public const int MaxDecodedBytes = 4 * 1024 * 1024;

        /// <summary>
        /// 이보다 긴 코드는 풀어 보지 않고 TooLarge (압축본이 MaxDecodedBytes를 넘으면 풀어도 넘는다 — 큰 클립보드를 base64로 다 풀지 않게).
        /// base64 4자 = 3바이트, 여유 4 KB
        /// </summary>
        const int MaxCodeChars = (MaxDecodedBytes + 4096) / 3 * 4 + 64;

        static uint[] _table;

        /// <summary>플레이어 데이터를 세이브 코드로 만든다</summary>
        public static string Export(PlayerData data)
        {
            if (data == null) return string.Empty;
            var json = JsonConvert.SerializeObject(data);
            var raw = Encoding.UTF8.GetBytes(json);
            byte[] packed;
            using (var output = new MemoryStream())
            {
                using (var gzip = new GZipStream(output, CompressionLevel.Optimal, true)) gzip.Write(raw, 0, raw.Length);
                packed = output.ToArray();
            }
            return $"{Prefix}{Crc32(packed):x8}.{Convert.ToBase64String(packed)}";
        }

        /// <summary>코드가 올바른 형식이고 체크섬이 맞으면 JSON을 돌려준다 (풀어 낸 크기 4 MB까지)</summary>
        public static bool TryDecode(string code, out string json) => Decode(code, out json) == SaveCodeError.None;

        /// <summary>
        /// 세이브 코드를 플레이어 데이터로 읽는다 (가져오기, SAVE-01 §4.2): 풀기 → 버전 확인(Sanitize 전) → 너그러운 JSON 읽기 → Sanitize, 요약도 돌려준다.
        /// 실패하면 false와 이유 (data = null, summary = 빈 요약). 예외를 던지지 않는다.
        /// 이유: 빈 코드 Empty · 머리말/점/체크섬 글자/base64가 틀림 BadFormat · 체크섬 다름 Checksum · 4 MB 넘음 TooLarge ·
        /// 더 새 버전 NewerVersion · 풀거나 읽지 못함 Corrupt
        /// </summary>
        public static bool TryParse(string code, out PlayerData data, out SaveCodeError error, out SaveSummary summary)
        {
            data = null;
            summary = default;
            try
            {
                error = Decode(code, out string json);
                if (error != SaveCodeError.None) return false;

                // 더 새 버전의 세이브는 이 빌드가 모르는 칸 때문에 읽다가 틀어질 수 있으므로 먼저 버전만 본다
                if (ReadVersion(json) > PlayerData.CurrentVersion)
                {
                    error = SaveCodeError.NewerVersion;
                    return false;
                }
                var parsed = JsonConvert.DeserializeObject<PlayerData>(json, LocalSaveStorage.ReadSettings);
                if (parsed == null)
                {
                    error = SaveCodeError.Corrupt;
                    return false;
                }
                if (parsed.version > PlayerData.CurrentVersion)
                {
                    error = SaveCodeError.NewerVersion;
                    return false;
                }
                parsed.Sanitize();
                data = parsed;
                summary = SaveSummary.From(parsed);
                error = SaveCodeError.None;
                return true;
            }
            catch (Exception)
            {
                data = null;
                summary = default;
                error = SaveCodeError.Corrupt;
                return false;
            }
        }

        /// <summary>요약이 필요 없을 때 (SPEC4 §4.2 모양, 위와 같음)</summary>
        public static bool TryParse(string code, out PlayerData data, out SaveCodeError error) => TryParse(code, out data, out error, out _);

        /// <summary>
        /// 가져오기 화면의 오류 줄 (SPEC4 §4.2): Checksum · BadFormat '코드가 잘렸거나 바뀌었어요…' · NewerVersion '더 새 버전에서…' ·
        /// TooLarge · Corrupt '이 코드는 읽을 수 없어요' · Empty '붙여 넣을 코드가 없어요…' · None 빈 문자열
        /// </summary>
        public static string ErrorText(SaveCodeError error) => error switch
        {
            SaveCodeError.None => string.Empty,
            SaveCodeError.Empty => "붙여 넣을 코드가 없어요. 먼저 세이브 코드를 복사해 주세요",
            SaveCodeError.BadFormat => "코드가 잘렸거나 바뀌었어요. 다시 복사해 주세요",
            SaveCodeError.Checksum => "코드가 잘렸거나 바뀌었어요. 다시 복사해 주세요",
            SaveCodeError.NewerVersion => "더 새 버전에서 만든 코드예요. 앱을 업데이트해 주세요",
            _ => "이 코드는 읽을 수 없어요",
        };

        /// <summary>코드를 풀어 JSON을 꺼낸다. 성공이면 None</summary>
        static SaveCodeError Decode(string code, out string json)
        {
            json = null;
            if (string.IsNullOrWhiteSpace(code)) return SaveCodeError.Empty;
            code = code.Trim();
            if (code.Length > MaxCodeChars) return SaveCodeError.TooLarge;
            if (!code.StartsWith(Prefix, StringComparison.Ordinal)) return SaveCodeError.BadFormat;
            int dot = code.IndexOf('.', Prefix.Length);
            if (dot - Prefix.Length != CrcHexLength) return SaveCodeError.BadFormat;
            if (!uint.TryParse(code.Substring(Prefix.Length, CrcHexLength), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture,
                    out uint expected))
                return SaveCodeError.BadFormat;

            byte[] packed;
            try
            {
                packed = Convert.FromBase64String(code.Substring(dot + 1));
            }
            catch (FormatException)
            {
                return SaveCodeError.BadFormat;
            }
            if (packed.Length == 0) return SaveCodeError.BadFormat;
            if (Crc32(packed) != expected) return SaveCodeError.Checksum;

            try
            {
                using var input = new MemoryStream(packed);
                using var gzip = new GZipStream(input, CompressionMode.Decompress);
                using var output = new MemoryStream();
                var buffer = new byte[16 * 1024];
                int read;
                while ((read = gzip.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (output.Length + read > MaxDecodedBytes) return SaveCodeError.TooLarge;
                    output.Write(buffer, 0, read);
                }
                if (output.Length == 0) return SaveCodeError.Corrupt;
                json = Encoding.UTF8.GetString(output.GetBuffer(), 0, (int)output.Length);
            }
            catch (Exception)
            {
                json = null;
                return SaveCodeError.Corrupt;
            }
            return string.IsNullOrWhiteSpace(json) ? SaveCodeError.Corrupt : SaveCodeError.None;
        }

        /// <summary>맨 바깥 객체의 "version" 숫자 (없거나 읽지 못하면 -1). 나머지는 건너뛴다</summary>
        static int ReadVersion(string json)
        {
            try
            {
                using var reader = new JsonTextReader(new StringReader(json));
                if (!reader.Read() || reader.TokenType != JsonToken.StartObject) return -1;
                while (reader.Read())
                {
                    if (reader.TokenType == JsonToken.EndObject) return -1;
                    if (reader.TokenType != JsonToken.PropertyName) return -1;
                    bool isVersion = string.Equals((string)reader.Value, "version", StringComparison.Ordinal);
                    if (!reader.Read()) return -1;
                    if (isVersion)
                    {
                        if (reader.TokenType == JsonToken.Integer) return (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, Convert.ToInt64(reader.Value, CultureInfo.InvariantCulture)));
                        return -1;
                    }
                    reader.Skip();
                }
            }
            catch (Exception)
            {
                // 형식이 틀리면 본 읽기가 Corrupt로 판정한다
            }
            return -1;
        }

        static uint Crc32(byte[] bytes)
        {
            _table ??= BuildTable();
            uint crc = 0xFFFFFFFFu;
            foreach (byte b in bytes) crc = _table[(crc ^ b) & 0xFF] ^ (crc >> 8);
            return ~crc;
        }

        static uint[] BuildTable()
        {
            var table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint c = i;
                for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                table[i] = c;
            }
            return table;
        }
    }
}
