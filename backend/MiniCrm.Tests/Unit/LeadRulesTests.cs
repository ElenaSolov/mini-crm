using MiniCrm.Api.Models;
using MiniCrm.Tests.Fakes;

namespace MiniCrm.Tests.Unit;

public class LeadRulesTests
{
    [Fact]
    public void New_lead_starts_as_new()
    {
        Assert.Equal(LeadStatusNames.New, new Lead().Status);
    }

    [Fact]
    public void Lead_with_contact_and_amount_can_be_converted()
    {
        var lead = TestData.ConvertibleLead();

        Assert.True(lead.TryChangeStatus(LeadStatusNames.Converted, out var error));
        Assert.Null(error);
        Assert.Equal(LeadStatusNames.Converted, lead.Status);
    }

    [Fact]
    public void Phone_alone_is_enough_contact_to_convert()
    {
        var lead = TestData.ConvertibleLead();
        lead.Email = null;

        Assert.True(lead.TryChangeStatus(LeadStatusNames.Converted, out _));
    }

    [Fact]
    public void Lead_without_email_and_phone_cannot_be_converted()
    {
        var lead = TestData.ConvertibleLead();
        lead.Email = null;
        lead.Phone = null;

        Assert.False(lead.TryChangeStatus(LeadStatusNames.Converted, out var error));
        Assert.Contains("email or a phone", error);
        Assert.Equal(LeadStatusNames.New, lead.Status);
    }

    [Fact]
    public void Lead_without_amount_cannot_be_converted()
    {
        var lead = TestData.ConvertibleLead();
        lead.Amount = null;

        Assert.False(lead.TryChangeStatus(LeadStatusNames.Converted, out var error));
        Assert.Contains("amount", error);
        Assert.Equal(LeadStatusNames.New, lead.Status);
    }

    [Fact]
    public void Lost_lead_cannot_be_converted()
    {
        var lead = TestData.ConvertibleLead();
        lead.TryChangeStatus(LeadStatusNames.Lost, out _);

        Assert.False(lead.TryChangeStatus(LeadStatusNames.Converted, out var error));
        Assert.Contains("lost", error);
        Assert.Equal(LeadStatusNames.Lost, lead.Status);
    }

    [Theory]
    [InlineData("new")]
    [InlineData("contacted")]
    [InlineData("lost")]
    public void Converted_lead_cannot_change_to_another_status(string newStatus)
    {
        var lead = TestData.ConvertibleLead();
        lead.TryChangeStatus(LeadStatusNames.Converted, out _);

        Assert.False(lead.TryChangeStatus(newStatus, out _));
        Assert.Equal(LeadStatusNames.Converted, lead.Status);
    }

    [Fact]
    public void Converted_lead_can_be_converted_again()
    {
        var lead = TestData.ConvertibleLead();
        lead.TryChangeStatus(LeadStatusNames.Converted, out _);

        Assert.True(lead.TryChangeStatus(LeadStatusNames.Converted, out _));
    }

    [Theory]
    [InlineData("new")]
    [InlineData("contacted")]
    [InlineData("qualified")]
    public void Lost_lead_cannot_change_to_another_status(string newStatus)
    {
        var lead = TestData.ConvertibleLead();
        lead.TryChangeStatus(LeadStatusNames.Lost, out _);

        Assert.False(lead.TryChangeStatus(newStatus, out var error));
        Assert.Contains("lost", error);
        Assert.Equal(LeadStatusNames.Lost, lead.Status);
    }

    [Fact]
    public void Converted_lead_must_keep_its_amount()
    {
        var lead = TestData.ConvertibleLead();
        lead.TryChangeStatus(LeadStatusNames.Converted, out _);

        Assert.False(lead.CanSetAmount(null, out _));
        Assert.True(lead.CanSetAmount(250m, out _));
    }

    [Fact]
    public void Lead_that_is_not_converted_can_clear_its_amount()
    {
        Assert.True(TestData.ConvertibleLead().CanSetAmount(null, out _));
    }
}
