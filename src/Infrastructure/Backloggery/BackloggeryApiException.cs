using System;

namespace PSNav.Infrastructure.Backloggery;

public sealed class BackloggeryApiException(string message) : Exception(message);
