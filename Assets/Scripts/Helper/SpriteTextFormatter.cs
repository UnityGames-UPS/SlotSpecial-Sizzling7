using System.Text;

public static class SpriteTextFormatter
{
    // Converts plain formatted number text (e.g. "4.8") into TMP sprite-asset tags
    // (e.g. "<sprite=4><sprite=10><sprite=8>") for fonts that render digits as sprites.
    // <sprite=0>..<sprite=9> are the digits, <sprite=10> is the decimal point.
    public static string ToSpriteDigits(string numberText)
    {
        var sb = new StringBuilder(numberText.Length * 10);
        foreach (char c in numberText)
        {
            if (c == '.')
            {
                sb.Append("<sprite=10>");
            }
            else if (c >= '0' && c <= '9')
            {
                sb.Append("<sprite=").Append(c - '0').Append('>');
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }
}
