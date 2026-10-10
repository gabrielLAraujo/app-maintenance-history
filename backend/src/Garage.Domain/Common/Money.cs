namespace Garage.Domain.Common;

public readonly record struct Money
{
    private const decimal MaxAmount = 9_999_999_999.99m;

    public Money(decimal amount)
    {
        if (!IsValid(amount))
            throw new ArgumentOutOfRangeException(nameof(amount), amount, $"O valor deve estar entre 0 e {MaxAmount} e ter no máximo 2 casas decimais.");

        Amount = amount;
    }

    public decimal Amount { get; }

    public static Money Zero { get; } = new(0m);

    public static bool IsValid(decimal amount) =>
        amount is >= 0m and <= MaxAmount && decimal.Round(amount, 2) == amount;

    public Money Times(decimal quantity) =>
        new(decimal.Round(Amount * quantity, 2, MidpointRounding.AwayFromZero));

    public static Money operator +(Money a, Money b) => new(a.Amount + b.Amount);
}
