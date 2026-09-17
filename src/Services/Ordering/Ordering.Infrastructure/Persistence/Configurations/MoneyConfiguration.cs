using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ordering.Domain.ValueObjects;

namespace Ordering.Infrastructure.Persistence.Configurations;

internal static class MoneyConfiguration
{
    public const int AmountPrecision = 12;

    /// <summary>Maps a <see cref="Money"/> complex property to <c>{prefix}_amount</c> and <c>{prefix}_currency</c>.</summary>
    public static ComplexPropertyBuilder<Money> MapMoney(this ComplexPropertyBuilder<Money> money, string columnPrefix)
    {
        money.Property(m => m.Amount)
            .HasColumnName($"{columnPrefix}_amount")
            .HasPrecision(AmountPrecision, Money.Scale);

        money.Property(m => m.Currency)
            .HasColumnName($"{columnPrefix}_currency")
            .HasMaxLength(Money.CurrencyLength)
            .IsFixedLength();

        return money;
    }
}
