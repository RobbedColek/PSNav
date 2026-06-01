using System.Collections.Generic;
using PSNav.Models.Accounts;

namespace PSNav.Repositories.Accounts;

public interface IAccountsRepository
{
    IReadOnlyList<Account> GetAll();
    Account? GetById(string accountId);
    void Upsert(Account account);
    void Update(Account account);
    void Delete(string accountId);
}