using Payment.Application.DTOs;
using Payment.Domain.ValueObjects;

namespace Payment.Application.Services;

public class SplitPaymentService : ISplitPaymentService
{
    public SplitPayment CalculateSplit(decimal totalAmount, decimal systemFeePercent)
    {
        return SplitPayment.Calculate(totalAmount, systemFeePercent);
    }

    public (SplitPayment SplitPayment, Dictionary<string, object> SplitDetails) CalculateMultiAccountSplit(
        decimal totalAmount, 
        SplitRuleDto splitRule)
    {
        if (totalAmount <= 0)
        {
            throw new ArgumentException("Total amount must be greater than zero", nameof(totalAmount));
        }

        if (splitRule == null)
        {
            throw new ArgumentNullException(nameof(splitRule));
        }

        if (splitRule.Accounts == null || splitRule.Accounts.Count == 0)
        {
            throw new ArgumentException("Split rule must contain at least one account", nameof(splitRule));
        }

        // Validate percentages total 100%
        var totalPercentage = splitRule.Accounts.Sum(a => a.Percentage);
        if (Math.Abs(totalPercentage - 100m) > 0.01m)
        {
            throw new ArgumentException($"Split percentages must total 100%, but total is {totalPercentage}%", nameof(splitRule));
        }

        // Calculate amounts for each account
        var accountSplits = splitRule.Accounts.Select(account => new
        {
            AccountType = account.AccountType,
            AccountIdentifier = account.AccountIdentifier,
            Percentage = account.Percentage,
            Amount = Math.Round(totalAmount * account.Percentage / 100, 2)
        }).ToList();

        // For backward compatibility, calculate system and owner shares
        // System share is the sum of all accounts marked as "SystemOwner" or similar
        var systemAccounts = accountSplits
            .Where(a => a.AccountType.Contains("System", StringComparison.OrdinalIgnoreCase))
            .ToList();
        
        var systemShare = systemAccounts.Any() 
            ? systemAccounts.Sum(a => a.Amount)
            : Math.Round(totalAmount * splitRule.SystemFeePercent / 100, 2);
        
        var ownerShare = totalAmount - systemShare;

        // Create the split payment value object (for backward compatibility)
        var splitPayment = new SplitPayment(
            systemShare,
            ownerShare,
            splitRule.SystemFeePercent);

        // Create detailed split information for metadata
        var splitDetails = new Dictionary<string, object>
        {
            { "SplitRule", new
                {
                    SystemFeePercent = splitRule.SystemFeePercent,
                    Accounts = accountSplits.Select(a => new
                    {
                        a.AccountType,
                        a.AccountIdentifier,
                        a.Percentage,
                        a.Amount
                    }).ToList()
                }
            },
            { "TotalAmount", totalAmount },
            { "SystemShare", systemShare },
            { "OwnerShare", ownerShare }
        };

        return (splitPayment, splitDetails);
    }
}

