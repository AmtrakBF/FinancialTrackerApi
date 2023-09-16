﻿namespace FinancialTracker.Common.Contracts.Account;

public record DeleteAccountRequest(
    Guid AccountId
    ) : AccountBase(AccountId);