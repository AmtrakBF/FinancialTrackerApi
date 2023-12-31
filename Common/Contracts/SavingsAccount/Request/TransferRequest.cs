﻿using System.ComponentModel.DataAnnotations;
using FinancialTracker.Common.Validators;

namespace FinancialTracker.Common.Contracts.SavingsAccount.Request;

public record TransferRequest(
    
    [Required(AllowEmptyStrings = false)]
    [Display(Name = "Account Id")]
    [Guid]
    string AccountId, 
    
    [Required(AllowEmptyStrings = false)]
    [Display(Name = "Date")]
    [DataType(DataType.Date)]
    DateTime Date,
    
    [Required(AllowEmptyStrings = false)]
    [Display(Name = "Recipient Account Id")]
    [Guid]
    string ReceiverAccountId, 
    
    [Required]
    [Display(Name = "Transfer Amount")]
    decimal TransferAmount, 
    
    [StringLength(maximumLength: 200)]
    string Description);