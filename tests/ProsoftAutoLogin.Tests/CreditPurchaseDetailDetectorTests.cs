using ProsoftAutoLogin.Automation;

namespace ProsoftAutoLogin.Tests;

public sealed class CreditPurchaseDetailDetectorTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(7, 7)]
    [InlineData(8, 7)]
    [InlineData(20, 7)]
    public void ViewportRowMapping_ClampsRowsBelowGridToLastVisibleRow(
        int documentRow,
        int expectedViewportRow)
    {
        Assert.Equal(expectedViewportRow, CreditPurchaseDetailDetector.GetViewportRowIndex(documentRow));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(6, 6)]
    [InlineData(7, 6)]
    [InlineData(8, 6)]
    [InlineData(20, 6)]
    public void RowAfterItemCodeCommit_AccountsForAutomaticDataWindowScroll(
        int documentRow,
        int expectedViewportRow)
    {
        Assert.Equal(
            expectedViewportRow,
            CreditPurchaseDetailDetector.GetViewportRowAfterItemCodeCommit(documentRow));
    }
}
