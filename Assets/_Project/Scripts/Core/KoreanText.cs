namespace IdleGame
{
    public static class KoreanText
    {
        /// <summary>받침 유무에 따라 주격 조사를 붙인다. 예) 골드가, 젬이</summary>
        public static string WithSubject(string word)
        {
            if (string.IsNullOrEmpty(word)) return word;
            char last = word[word.Length - 1];
            if (last < 0xAC00 || last > 0xD7A3) return word + "이(가)";
            return word + ((last - 0xAC00) % 28 != 0 ? "이" : "가");
        }
    }
}
