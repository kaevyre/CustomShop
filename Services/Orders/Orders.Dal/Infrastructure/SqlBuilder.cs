using System.Text.RegularExpressions;

namespace Orders.Dal.Infrastructure;

/// <summary>
/// Допоміжні методи для побудови SQL загальних CRUD-запитів.
/// Складаються ЛИШЕ ідентифікатори (таблиці/стовпці), які задані в коді репозиторіїв і додатково
/// перевіряються регулярним виразом. Значення з запитів користувача в SQL-текст не потрапляють —
/// вони передаються виключно параметрами (@Name).
/// </summary>
internal static partial class SqlBuilder
{
    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex IdentifierPattern();

    public static string Identifier(string name)
    {
        if (!IdentifierPattern().IsMatch(name))
            throw new ArgumentException($"'{name}' is not a valid SQL identifier.", nameof(name));
        return "[" + name + "]";
    }

    public static string Table(string schema, string table) => Identifier(schema) + "." + Identifier(table);

    public static string ColumnList(IEnumerable<string> columns) =>
        string.Join(", ", columns.Select(Identifier));

    public static string ParameterList(IEnumerable<string> columns) =>
        string.Join(", ", columns.Select(c => "@" + Identifier(c).Trim('[', ']')));

    public static string SetList(IEnumerable<string> columns) =>
        string.Join(", ", columns.Select(c => Identifier(c) + " = @" + c));
}
