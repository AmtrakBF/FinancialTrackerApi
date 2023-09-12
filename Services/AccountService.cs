﻿using Dapper;
using FinancialTracker.Models;
using FinancialTracker.Persistance;
using FinancialTracker.Services.Common;
using System.Data;
using FinancialTracker.Common.Contracts.SavingsAccount.Request;
using FinancialTracker.Common.Contracts.SavingsAccount.Response;
using FinancialTracker.Models.Enums;
using FinancialTracker.Services.Records;

namespace FinancialTracker.Services
{
    public interface IAccountService
    {
        Task<Account> GetSavingsAccount(string accountId);
        Task<IEnumerable<Account>> GetSavingsAccounts();
        Task<Account> OpenSavingsAccount(OpenAccountRequest request);
        Task<Account> CloseSavingsAccount(CloseAccountRequest request);
        Task<TransactionResponse> AddTransaction(AddTransactionRequest request);
        Task<TransferResponse> TransferToAccount(TransferRequest request);
        Task<IEnumerable<Transaction>> GetAccountTransactions(string accountId, int offset, int rowLimit);
        Task<Account> ChangeAccountName(AccountNameChangeRequest request);
        Task<Transaction> EditTransaction(EditTransactionRequest request);
        Task<Transaction> DeleteTransaction(DeleteTransactionRequest request);
        Task<TransactionTypesSumFromRangeResponse> GetTransactionSumsFromRange(TransactionTypesSumFromRangeRequest request);
    }

    public class AccountService : IAccountService
    {
        private readonly ISqlDataAccess _sqlDataAccess;
        private readonly IHttpContextHelperService _httpContext;
        private readonly IUserService _userService;

        public AccountService(ISqlDataAccess sqlDataAccess, IHttpContextHelperService httpContext, IUserService userService)
        {
            _sqlDataAccess = sqlDataAccess;
            _httpContext = httpContext;
            _userService = userService;
        }

        public async Task<Account> GetSavingsAccount(string accountId)
        {
            var userId = _httpContext.GetClaimUserId().Value;
            var savingsAccount = (await _sqlDataAccess.GetConnection().QueryAsync<Account>("GetSavingsAccount",
                    new { @id = accountId, @userid = userId }, commandType: CommandType.StoredProcedure))
                .FirstOrDefault();

            return savingsAccount ?? throw new Exception("The provided account id is invalid.");
        }

        public async Task<IEnumerable<Account>> GetSavingsAccounts()
        {
            var userId = _httpContext.GetClaimUserId();
            var savingsAccounts = (await _sqlDataAccess.GetConnection().QueryAsync<Account>
            ("GetSavingsAccounts", new { @id = userId.Value }, commandType: CommandType.StoredProcedure)).ToList();

            return savingsAccounts.Any() ? savingsAccounts : throw new Exception("Cannot find savings accounts");
        }

        public async Task<Account> OpenSavingsAccount(OpenAccountRequest request)
        {
            var userId = _httpContext.GetClaimUserId();
            var account = Account.CreateNew(userId.Value, request.AccountName, request.InitialBalance);
            await _sqlDataAccess.GetConnection().ExecuteAsync("OpenSavingsAccount", account, commandType: CommandType.StoredProcedure);
            return account;
        }

        public async Task<Account> CloseSavingsAccount(CloseAccountRequest request)
        {
            await _userService.VerifyCredentialsAsync(request.LoginRequest);
            
            var account = await GetSavingsAccount(request.AccountId);
            if (account.Balance != 0) throw new Exception("Savings account balance must be 0.00");

            var result = await _sqlDataAccess.GetConnection().ExecuteAsync("DeleteSavingsAccount",
                new { @accountId = account.Id, @userId = account.UserId },
                commandType: CommandType.StoredProcedure);

            return result < 0 ? throw new Exception("Savings account balance must be 0.00") : account;
        }

        public async Task<TransactionResponse> AddTransaction(AddTransactionRequest request)
        {
            var account = await GetSavingsAccount(request.AccountId);

            var transaction = Transaction.CreateNewTransaction(request.Type, request.Description, request.Amount,
                request.Date, account);

            await _sqlDataAccess.GetConnection()
                .ExecuteAsync("AddTransaction", transaction, commandType: CommandType.StoredProcedure);

            return new TransactionResponse(transaction, account);
        }

        public async Task<TransferResponse> TransferToAccount(TransferRequest request)
        {
            var transferInAccount = await GetSavingsAccount(request.ReceiverAccountId);
            var transferOutAccount = await GetSavingsAccount(request.AccountId);
            
            if (transferInAccount.UserId != transferOutAccount.UserId)
                throw new Exception("Accounts used in transfer must be the same.");

            var transferInTransaction = Transaction.CreateNewTransaction(TransactionType.TransferIn,
                request.Description, request.TransferAmount, request.Date, transferInAccount);

            var transferOutTransaction = Transaction.CreateNewTransaction(TransactionType.TransferOut,
                request.Description, request.TransferAmount, request.Date, transferOutAccount);
            
            //var transfer = ConvertToTransfer(transferInTransaction, transferOutTransaction, DateTime.UtcNow);
            var transfer = new { };
            
            var result = await _sqlDataAccess.GetConnection()
                .ExecuteAsync("TransferToAccount", transfer, commandType: CommandType.StoredProcedure);
            
            return result < 0 ? throw new Exception("Cannot complete transfer") :
                new TransferResponse(transferOutTransaction, transferInTransaction);
        }

        public async Task<IEnumerable<Transaction>> GetAccountTransactions(string accountId, int offset, int rowLimit)
        {
            var userId = _httpContext.GetClaimUserId().Value;
            return await _sqlDataAccess.GetConnection()
                .QueryAsync<Transaction>("GetTransactionsFromAccount", new { @accountId, @userId, @offset, @rowLimit },
                    commandType: CommandType.StoredProcedure);
        }

        public async Task<Account> ChangeAccountName(AccountNameChangeRequest request)
        {
            var account = await GetSavingsAccount(request.AccountId);
            account.ChangeName(request.Name);
            await _sqlDataAccess.GetConnection().ExecuteAsync("UpdateAccountName",
                new { request.AccountId, account.UserId, request.Name }, commandType: CommandType.StoredProcedure);
            return account;
        }

        public async Task<Transaction> EditTransaction(EditTransactionRequest request)
        {
            var account = await GetSavingsAccount(request.AccountId);
            var initialTransaction = await GetTransaction(account.Id, request.TransactionId);

            if (initialTransaction.TransactionType == "Transfer")
                throw new Exception("Transactions of type transfer are not editable.");
            
            var type = Transaction.StringToTransactionType(initialTransaction.TransactionType);
            var newTransaction = Transaction.CreateExistingTransaction(initialTransaction.Id, initialTransaction.SavingsAccountId, 
                type, request.Description, initialTransaction.Amount, initialTransaction.NewBalance, request.Date);
            
            await _sqlDataAccess.GetConnection()
                .ExecuteAsync("EditTransaction",
                    new
                    {
                        initialTransaction.Id, initialTransaction.SavingsAccountId, request.Description, request.Date
                    }, commandType: CommandType.StoredProcedure);

            return newTransaction;
        }

        public async Task<Transaction> DeleteTransaction(DeleteTransactionRequest request)
        {
            var account = await GetSavingsAccount(request.SavingsAccountId);
            var transaction = await GetTransaction(account.Id, request.TransactionId);

            if (transaction.TransactionType is "TransferIn" or "TransferOut")
                throw new Exception("Transaction deleted cannot be a transfer.");
            
            switch (transaction.TransactionType)
            {
                case "Deposit": 
                case "TransferIn":
                    account.Withdraw(transaction.Amount);
                    break;
                case "Withdrawal": 
                case "TransferOut":
                    account.Deposit(transaction.Amount);
                    break;
            }

            transaction.SetBalance(account);
            
            await _sqlDataAccess.GetConnection().ExecuteAsync("DeleteTransaction",
            new { transaction.Id, transaction.SavingsAccountId, newBalance = account.Balance },
            commandType: CommandType.StoredProcedure);

            return transaction;
        }

        public async Task<TransactionTypesSumFromRangeResponse> GetTransactionSumsFromRange(TransactionTypesSumFromRangeRequest request)
        {
            var account = await GetSavingsAccount(request.SavingsAccountId);
            var transactions = (await _sqlDataAccess.GetConnection().QueryAsync<Transaction>("GetTransactionsInRange",
                new { @savingsAccountId = account.Id, request.StartDate, request.EndDate },
                commandType: CommandType.StoredProcedure)).ToList();

            decimal depositSum = 0, withdrawalSum = 0, transferInSum = 0, transferOutSum = 0;
            
            foreach (var transaction in transactions)
            {
                switch (transaction.TransactionType)
                {
                    case "Deposit": { depositSum += transaction.Amount; }
                        break;
                    case "Withdrawal": { withdrawalSum += transaction.Amount; }
                        break;
                    case "TransferIn": { transferInSum += transaction.Amount; }
                        break;
                    case "TransferOut": { transferOutSum += transaction.Amount; }
                        break;
                }
            }
            
            return new TransactionTypesSumFromRangeResponse(depositSum,withdrawalSum,transferInSum,transferOutSum);
        }

        // private static Transfer ConvertToTransfer(Transaction transactionIn,
        //     Transaction transactionOut, DateTime date, string? transferId = null)
        // {
        //     transferId ??= Guid.NewGuid().ToString();
        //
        //     if (transactionIn.Amount != transactionOut.Amount)
        //         throw new Exception("Transfer failed.");
        //     
        //     return new Transfer(transferId, transactionOut.Id, transactionOut.SavingsAccountId,
        //         transactionOut.NewBalance, transactionOut.TransactionType, transactionIn.Id,
        //         transactionIn.SavingsAccountId, transactionIn.NewBalance, transactionIn.TransactionType,
        //         transactionIn.Amount, date, transactionOut.Description);
        // }

        private async Task<Transaction> GetTransaction(string accountId, string transactionId)
        {
             var response = (await _sqlDataAccess.GetConnection()
                .QueryAsync<Transaction>("GetTransaction", new { @accountId, @transactionId }, commandType: CommandType.StoredProcedure)).FirstOrDefault();
             if (response == null)
                 throw new Exception("The provided transaction id is invalid.");
             return response;
        }
    }
}