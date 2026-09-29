using ProsoftAutoLogin.Data;

namespace ProsoftAutoLogin.Tests;

public sealed class VendorCsvFilterTests
{
    [Fact]
    public void FilterApprovedDocuments_ReturnsOnlyPendingRequiredStatus()
    {
        var documents = new[]
        {
            new VendorCsvRecord("A", "001", Status: " Approved "),
            new VendorCsvRecord("B", "002", Status: "approved", ResultStatus: "Completed"),
            new VendorCsvRecord("C", "003", Status: "Pending")
        };

        var result = VendorCsvReader.FilterApprovedDocuments(documents, "approved");

        var document = Assert.Single(result);
        Assert.Equal("001", document.VendorCode);
    }
}
