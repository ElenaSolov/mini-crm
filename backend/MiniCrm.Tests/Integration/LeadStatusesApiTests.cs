using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using MiniCrm.Api.Dtos;
using MiniCrm.Api.Models;

namespace MiniCrm.Tests.Integration;

// The LeadStatuses table. These need SQL Server: EF InMemory has no foreign keys
// and compares text with exact case.
public class LeadStatusesApiTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Statuses_endpoint_lists_the_tasks_statuses_in_order()
    {
        var statuses = await Client.GetFromJsonAsync<List<string>>("/api/leads/statuses", Json);

        Assert.Equal(["new", "contacted", "qualified", "converted", "lost"], statuses);
    }

    [Fact]
    public async Task Unknown_status_is_400_on_the_status_field()
    {
        var lead = await CreateLead();

        var response = await ChangeStatus(lead.Id, "won");

        Assert.Equal(["status"], await ErrorFields(response));
    }

    [Fact]
    public async Task Status_is_found_in_any_case_and_saved_as_the_table_has_it()
    {
        var lead = await CreateLead();

        var response = await ChangeStatus(lead.Id, "QUALIFIED");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<LeadResponse>(Json);
        Assert.Equal("qualified", updated!.Status);
    }

    [Fact]
    public async Task Upper_case_converted_still_queues_the_event()
    {
        var lead = await CreateLead();

        await ChangeStatus(lead.Id, "Converted");

        Assert.Single(await GetEvents(lead.Id));
    }

    [Fact]
    public async Task New_status_row_works_without_a_code_change()
    {
        await WithDb(async db =>
        {
            db.LeadStatuses.Add(new LeadStatus { Name = "negotiation", SortOrder = 6 });
            await db.SaveChangesAsync();
        });
        var lead = await CreateLead();

        var statuses = await Client.GetFromJsonAsync<List<string>>("/api/leads/statuses", Json);
        var response = await ChangeStatus(lead.Id, "negotiation");

        Assert.Equal("negotiation", statuses!.Last());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("negotiation", (await response.Content.ReadFromJsonAsync<LeadResponse>(Json))!.Status);
    }

    [Fact]
    public async Task Status_in_use_cannot_be_deleted()
    {
        var lead = await CreateLead();
        await ChangeStatus(lead.Id, "qualified");

        await WithDb(async db =>
        {
            db.LeadStatuses.Remove(await db.LeadStatuses.SingleAsync(s => s.Name == "qualified"));

            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        });
    }
}
