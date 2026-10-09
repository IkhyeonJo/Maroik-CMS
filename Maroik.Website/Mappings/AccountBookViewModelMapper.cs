using System.Globalization;
using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Time;
using Maroik.Website.Models.ViewModels.AccountBook;
// ReSharper disable InvalidXmlDocComment

namespace Maroik.Website.Mappings;

/// <summary>
/// Maps account-book service response objects to display view models.
/// Applies timezone conversion to timestamps and localization to enumeration display values.
/// Keeping this logic here (rather than in the controller) makes it independently testable
/// and prevents the controller from accumulating presentation-layer responsibilities.
/// </summary>
public static class AccountBookViewModelMapper
{
    /// <summary>The "yyyy-MM-dd HH:mm:ss" shape <see cref="ParseLocalDateTime"/> expects.</summary>
    private const string LocalDateTimeFormat = "yyyy-MM-dd HH:mm:ss";

    /// <summary>
    /// True when <paramref name="value"/> parses as <see cref="LocalDateTimeFormat"/> — used by
    /// <see cref="ExpenditureInputViewModel.Validate"/> / <see cref="IncomeInputViewModel.Validate"/>
    /// so a malformed <c>Created</c> value fails cleanly as a validation error instead of throwing
    /// out of the <c>ToCreatedUtc</c> extensions below.
    /// </summary>
    public static bool IsValidLocalDateTime(string? value) =>
        DateTime.TryParseExact(value, LocalDateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    /// <summary>
    /// Parses a "yyyy-MM-dd HH:mm:ss" local wall-clock string (no time zone of its own — the
    /// caller applies one via <see cref="DateTimeExtensions.ConvertToUtcByTimeZoneIanaId"/>).
    /// </summary>
    private static DateTime ParseLocalDateTime(string value) =>
        DateTime.ParseExact(value, LocalDateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None);

    extension(IncomeInputViewModel vm)
    {
        /// <summary>
        /// Converts <see cref="IncomeInputViewModel.Created"/> — the account's own local wall-clock
        /// time, never the browser's — to UTC using <paramref name="accountTimeZoneIanaId"/> (the
        /// logged-in account's own <c>TimeZoneIanaId</c>, already known server-side). The client
        /// never computes or transmits a UTC instant or a time zone itself.
        /// </summary>
        public DateTime ToCreatedUtc(string accountTimeZoneIanaId) =>
            ParseLocalDateTime(vm.Created!).ConvertToUtcByTimeZoneIanaId(accountTimeZoneIanaId);
    }

    extension(ExpenditureInputViewModel vm)
    {
        /// <summary>Same conversion as the <c>IncomeInputViewModel</c> overload above, for an expenditure.</summary>
        public DateTime ToCreatedUtc(string accountTimeZoneIanaId) =>
            ParseLocalDateTime(vm.Created!).ConvertToUtcByTimeZoneIanaId(accountTimeZoneIanaId);
    }

    extension(AssetResponse item)
    {
        /// <summary>Maps a single <see cref="AssetResponse"/> to an <see cref="AssetOutputViewModel"/> for display.</summary>
        public AssetOutputViewModel ToDisplayViewModel(Func<string, string> localize, string timeZoneIanaId) => new()
        {
            ProductName = item.ProductName,
            Item = localize(item.Item ?? ""),
            Amount = item.Amount,
            MonetaryUnit = item.MonetaryUnit,
            Created = item.Created.ConvertTimeByTimeZoneIanaId(timeZoneIanaId),
            Updated = item.Updated.ConvertTimeByTimeZoneIanaId(timeZoneIanaId),
            Note = item.Note,
            Deleted = item.Deleted
        };
    }

    extension(IEnumerable<AssetResponse> items)
    {
        /// <summary>Maps all <see cref="AssetResponse"/> items to display view models.</summary>
        public List<AssetOutputViewModel> ToDisplayViewModels(Func<string, string> localize, string timeZoneIanaId)
            =>
            [
                .. items.Select(i => i.ToDisplayViewModel(localize, timeZoneIanaId))
            ];
    }

    extension(IncomeResponse item)
    {
        /// <summary>
        /// Maps a single <see cref="IncomeResponse"/> to an <see cref="IncomeOutputViewModel"/> for display.
        /// <paramref name="currencyByProduct"/> maps asset product names to their monetary unit
        /// and is used to populate <see cref="IncomeOutputViewModel.MonetaryUnit"/>.
        /// </summary>
        public IncomeOutputViewModel ToDisplayViewModel(
            IReadOnlyDictionary<string, string?> currencyByProduct,
            Func<string, string> localize,
            string timeZoneIanaId) => new()
            {
                Id = item.Id,
                MainClass = localize(item.MainClass ?? ""),
                SubClass = localize(item.SubClass ?? ""),
                Content = item.Content,
                Amount = item.Amount,
                MonetaryUnit = currencyByProduct.GetValueOrDefault(item.DepositMyAssetProductName ?? ""),
                DepositMyAssetProductName = item.DepositMyAssetProductName,
                Created = item.Created.ConvertTimeByTimeZoneIanaId(timeZoneIanaId),
                Updated = item.Updated.ConvertTimeByTimeZoneIanaId(timeZoneIanaId),
                Note = item.Note
            };
    }

    extension(IEnumerable<IncomeResponse> items)
    {
        /// <summary>Maps all <see cref="IncomeResponse"/> items to display view models.</summary>
        public List<IncomeOutputViewModel> ToDisplayViewModels(
            IEnumerable<AssetResponse> assets,
            Func<string, string> localize,
            string timeZoneIanaId)
        {
            var currencyByProduct = assets
                .GroupBy(a => a.ProductName ?? "")
                .ToDictionary(g => g.Key, g => g.Last().MonetaryUnit);
            return
            [
                .. items.Select(i => i.ToDisplayViewModel(currencyByProduct, localize, timeZoneIanaId))
            ];
        }
    }

    extension(ExpenditureResponse item)
    {
        /// <summary>
        /// Maps a single <see cref="ExpenditureResponse"/> to an <see cref="ExpenditureOutputViewModel"/> for display.
        /// <paramref name="currencyByProduct"/> maps asset product names to their monetary unit.
        /// </summary>
        public ExpenditureOutputViewModel ToDisplayViewModel(
            IReadOnlyDictionary<string, string?> currencyByProduct,
            Func<string, string> localize,
            string timeZoneIanaId) => new()
            {
                Id = item.Id,
                MainClass = localize(item.MainClass ?? ""),
                SubClass = localize(item.SubClass ?? ""),
                Content = item.Content,
                Amount = item.Amount,
                MonetaryUnit = currencyByProduct.GetValueOrDefault(item.PaymentMethod ?? ""),
                PaymentMethod = item.PaymentMethod,
                Note = item.Note,
                MyDepositAsset = item.MyDepositAsset,
                Created = item.Created.ConvertTimeByTimeZoneIanaId(timeZoneIanaId),
                Updated = item.Updated.ConvertTimeByTimeZoneIanaId(timeZoneIanaId)
            };
    }

    extension(IEnumerable<ExpenditureResponse> items)
    {
        /// <summary>Maps all <see cref="ExpenditureResponse"/> items to display view models.</summary>
        public List<ExpenditureOutputViewModel> ToDisplayViewModels(
            IEnumerable<AssetResponse> assets,
            Func<string, string> localize,
            string timeZoneIanaId)
        {
            var currencyByProduct = assets
                .GroupBy(a => a.ProductName ?? "")
                .ToDictionary(g => g.Key, g => g.Last().MonetaryUnit);
            return
            [
                .. items.Select(i => i.ToDisplayViewModel(currencyByProduct, localize, timeZoneIanaId))
            ];
        }
    }

    extension(AccountBookPageViewModel vm)
    {
        /// <summary>
        /// Fills the current local time fields on <paramref name="vm"/> for pre-populating date/time pickers:
        /// <paramref name="utcNow"/> (the request's clock reading) shown in <paramref name="timeZoneIanaId"/>.
        /// </summary>
        public void PopulateTimeData(string timeZoneIanaId, DateTime utcNow)
        {
            DateTime ct = utcNow.ConvertTimeByTimeZoneIanaId(timeZoneIanaId);
            vm.CurrentDate = ct.ToString("yyyy-MM-dd");
            vm.CurrentHour = ct.Hour;
            vm.CurrentMinute = ct.Minute;
            vm.CurrentSecond = ct.Second;
        }
    }
}
