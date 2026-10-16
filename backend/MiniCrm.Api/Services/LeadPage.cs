using MiniCrm.Api.Models;

namespace MiniCrm.Api.Services;

// One page of leads, plus how many leads match the filters in total.
public record LeadPage(List<Lead> Items, int TotalCount);
