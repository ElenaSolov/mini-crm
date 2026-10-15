using Microsoft.EntityFrameworkCore;

namespace MiniCrm.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
}
