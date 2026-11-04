namespace GS1CodeStream.Server;

/// <summary>
/// Создаёт синтетические коды с имитацией структуры GS1.
/// </summary>
public sealed class MarkingCodeGenerator
{
    private const string SerialCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    private const int SerialLength = 12;

    /// <summary>
    /// Генерирует код с GTIN-14 и случайным серийным номером.
    /// </summary>
    /// <returns>Человекочитаемый синтетический код.</returns>
    public string Generate()
    {
        Span<char> gtin = stackalloc char[14];
        for (int i = 0; i < 13; i++)
        {
            gtin[i] = (char)('0' + Random.Shared.Next(10));
        }

        gtin[13] = CalculateCheckDigit(gtin[..13]);

        Span<char> serial = stackalloc char[SerialLength];
        for (int i = 0; i < serial.Length; i++)
        {
            serial[i] = SerialCharacters[Random.Shared.Next(SerialCharacters.Length)];
        }

        return $"(01){gtin}(21){serial}";
    }

    /// <summary>
    /// Вычисляет контрольную цифру для цифр GTIN без контрольного разряда.
    /// </summary>
    internal static char CalculateCheckDigit(ReadOnlySpan<char> digits)
    {
        int sum = 0;
        for (int i = digits.Length - 1, weight = 3; i >= 0; i--, weight = 4 - weight)
        {
            sum += (digits[i] - '0') * weight;
        }

        return (char)('0' + (10 - sum % 10) % 10);
    }
}
