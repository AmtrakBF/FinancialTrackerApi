﻿using FinancialTracker.Common.Contracts.Common;

namespace FinancialTracker.Common.Contracts.Account;

public record DeleteAccountRequest(
    Guid AccountId
    ) : IAuthenticatedRequest;