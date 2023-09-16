﻿namespace FinancialTracker.Common.Contracts.Account;

public record GetAccountRequest(
    Guid AccountId
    ) : AccountBase(AccountId);