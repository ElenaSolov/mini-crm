using System.ComponentModel.DataAnnotations;
using MiniCrm.Api.Dtos;

namespace MiniCrm.Tests.Unit;

// The input checks that run before a lead is saved (they become a 400).
public class SaveLeadRequestTests
{
    private static List<ValidationResult> Validate(SaveLeadRequest request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
        return results;
    }

    private static SaveLeadRequest ValidRequest() => new()
    {
        Name = "Ann",
        Email = "lead@example.com",
        Amount = 199m,
        Currency = "USD"
    };

    [Fact]
    public void Valid_request_has_no_errors()
    {
        Assert.Empty(Validate(ValidRequest()));
    }

    [Fact]
    public void Name_is_required()
    {
        var request = ValidRequest();
        request.Name = "   ";

        Assert.NotEmpty(Validate(request));
    }

    [Fact]
    public void Email_or_phone_is_required()
    {
        var request = ValidRequest();
        request.Email = null;

        var error = Assert.Single(Validate(request));
        Assert.Equal(["email", "phone"], error.MemberNames);
    }

    [Fact]
    public void Empty_email_with_a_phone_is_valid()
    {
        var request = ValidRequest();
        request.Email = "";
        request.Phone = "+12025550123";

        Assert.Empty(Validate(request));
        Assert.Null(request.Email);
    }

    [Fact]
    public void Invalid_email_is_rejected()
    {
        var request = ValidRequest();
        request.Email = "not-an-email";

        Assert.NotEmpty(Validate(request));
    }

    [Fact]
    public void Currency_is_required_when_amount_is_set()
    {
        var request = ValidRequest();
        request.Currency = null;

        var error = Assert.Single(Validate(request));
        Assert.Equal(["currency"], error.MemberNames);
    }

    [Theory]
    [InlineData("US")]
    [InlineData("USDD")]
    [InlineData("U5D")]
    public void Currency_must_be_3_letters(string currency)
    {
        var request = ValidRequest();
        request.Currency = currency;

        Assert.NotEmpty(Validate(request));
    }

    [Fact]
    public void Amount_with_more_than_2_decimals_is_rejected()
    {
        var request = ValidRequest();
        request.Amount = 1.999m;

        Assert.NotEmpty(Validate(request));
    }

    [Fact]
    public void Negative_amount_is_rejected()
    {
        var request = ValidRequest();
        request.Amount = -1m;

        Assert.NotEmpty(Validate(request));
    }

    [Fact]
    public void Values_are_cleaned()
    {
        var request = new SaveLeadRequest
        {
            Name = "  Ann  ",
            Email = " lead@example.com ",
            Source = " Facebook ",
            Currency = "usd"
        };

        Assert.Equal("Ann", request.Name);
        Assert.Equal("lead@example.com", request.Email);
        Assert.Equal("facebook", request.Source);
        Assert.Equal("USD", request.Currency);
    }
}
