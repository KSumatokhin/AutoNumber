using System.Text.RegularExpressions;

namespace AutoNumber.Services
{
    internal static class MTextFormatService
    {
        public static string RemoveFormatting(string value, bool preserveMTextEscapes = false)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            string result = Regex.Replace(value, @"\\\\", "\u001A");
            result = Regex.Replace(result, @"\\P|\r\n|\n|\t", " ");
            result = Regex.Replace(result, @"\\[ACcFfHLlOopQTW][^\\;]*;|\\[ACcFfHLlOopQTW]", string.Empty);
            result = Regex.Replace(result, @"([^\\])\\S([^;]*)[/#\^]([^;]*);", "$1$2/$3");
            result = Regex.Replace(result, @"\\(\\S)|[\\](})|}", "$1$2");
            result = Regex.Replace(result, @"[\\]({)|{", "$1");
            result = Regex.Replace(result, @"\s{2,}", " ").Trim();

            return preserveMTextEscapes
                ? result.Replace("\u001A", "\\\\")
                : result.Replace("\u001A", "\\");
        }
    }
}
