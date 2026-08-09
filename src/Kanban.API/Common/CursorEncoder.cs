namespace Kanban.API.Common;

public static class CursorEncoder
{
    public static string Encode(string value) =>
        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value));

    public static bool TryDecode(string cursor, out string? value)
    {
        try
        {
            value = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            return true;
        }
        catch (FormatException)
        {
            value = null;
            return false;
        }
    }
}
