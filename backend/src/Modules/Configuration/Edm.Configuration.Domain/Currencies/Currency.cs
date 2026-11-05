using System.Globalization;

namespace Edm.Configuration.Domain.Currencies;

public sealed record Currency(
    Guid Id,
    Guid TenantId,
    string Code,
    string Name,
    bool IsBase,
    bool IsActive,
    long RowVersion,
    DateTimeOffset UpdatedAtUtc,
    Guid? UpdatedBy);

public sealed record CurrencyRate(
    Guid Id,
    Guid TenantId,
    Guid CurrencyId,
    decimal Rate,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    bool IsActive,
    long RowVersion,
    DateTimeOffset UpdatedAtUtc,
    Guid? UpdatedBy);

public sealed record CurrencyView(Currency Currency, CurrencyRate? ActiveRate);

public static class CurrencyRules
{
    public const int CodeLength = 3;
    public const int NameMaxLength = 100;
    public const decimal MaximumRate = 99_999_999_999_999.9999m;

    public static string? CodeError(string? code)
    {
        var value = code?.Trim() ?? string.Empty;
        return value.Length == CodeLength && value.All(char.IsAsciiLetter)
            ? null
            : "Enter a three-letter currency code, for example USD.";
    }

    public static string? NameError(string? name)
    {
        var value = name?.Trim() ?? string.Empty;
        if (value.Length == 0)
        {
            return "Enter a currency name.";
        }

        return value.Length > NameMaxLength
            ? $"The currency name must be {NameMaxLength} characters or fewer."
            : null;
    }

    public static string? RateError(string? rate)
    {
        if (!TryParseRate(rate, out var value))
        {
            return "Enter a valid PKR conversion rate.";
        }

        if (value <= 0)
        {
            return "The rate must be greater than zero.";
        }

        if (decimal.Round(value, 4) != value)
        {
            return "Use at most four decimal places.";
        }

        return value > MaximumRate ? "The rate is too large." : null;
    }

    public static bool TryParseRate(string? rate, out decimal value)
        => decimal.TryParse(
            rate?.Trim(),
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out value);

    public static string NormaliseCode(string code) => code.Trim().ToUpperInvariant();

    public static string NormaliseName(string name) => name.Trim();
}
