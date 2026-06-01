using Microsoft.Data.Sqlite;

namespace PSNav.Infrastructure.Database;

public interface IDatabaseService
{
    SqliteConnection GetConnection();
}