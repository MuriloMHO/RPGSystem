using RPGSystem.Characters.Players.PlayerClasses;
using Xunit;

namespace RPGSystem.Test;

public class PlayerGoldTests
{
    [Theory]
    [InlineData(50, 20, true, 30)]
    [InlineData(50, 50, true, 0)]
    [InlineData(50, 51, false, 50)]
    [InlineData(50, 0, true, 50)]
    [InlineData(0, 0, true, 0)]
    [InlineData(0, 1, false, 0)]
    public void TrySpendGold_ValidAmountsRespectAffordability(
        int initialGold, int amount, bool expectedSuccess, int expectedRemainingGold)
    {
        var knight = TestState.Player(new Knight("Gold Tests"));
        knight.GainGold(initialGold);

        Assert.Equal(expectedSuccess, knight.TrySpendGold(amount));
        Assert.Equal(expectedRemainingGold, knight.Gold);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void TrySpendGold_NegativeAmountThrowsWithoutChangingBalance(int amount)
    {
        var knight = TestState.Player(new Knight("Negative Gold Tests"));
        knight.GainGold(50);

        Assert.Throws<ArgumentOutOfRangeException>(() => knight.TrySpendGold(amount));
        Assert.Equal(50, knight.Gold);
    }
}
