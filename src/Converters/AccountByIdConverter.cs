using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Data.Converters;
using PSNav.Models.Accounts;

namespace PSNav.Converters;

public class AccountByIdConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        return values is [string accountId, IEnumerable<Account> accounts, ..]
            ? accounts.FirstOrDefault(a => a.AccountId == accountId)
            : null;
    }
}