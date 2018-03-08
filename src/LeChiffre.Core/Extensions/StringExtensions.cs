namespace LeChiffre.Core.Extensions
{
    public static class StringExtensions
    {
        // From: https://stackoverflow.com/a/2776689/5018
        public static string Truncate(this string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }
    }
}
