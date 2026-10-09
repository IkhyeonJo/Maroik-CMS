using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Finance;
using Maroik.Core.Domain.Time;
using Maroik.Website.Models.ViewModels.Notice;

namespace Maroik.Website.Mappings;

/// <summary>
/// Maps fixed-income and fixed-expenditure response objects to display view models.
/// Schedule status flags (<c>Noticed</c>, <c>Expired</c>) are computed via
/// <see cref="FixedSchedulePolicy"/>, which is shared with the service layer.
/// </summary>
public static class NoticeViewModelMapper
{
    extension(FixedScheduleRowStatus status)
    {
        /// <summary>
        /// Maps a <see cref="FixedScheduleRowStatus"/> to the notice-grid's Bootstrap row class.
        /// The status decision is Domain's (<see cref="FixedSchedulePolicy.GetRowStatus"/>); this
        /// presentation-only mapping (and the "clsGridRow" selector hook the client script binds
        /// to) belongs here, not in Domain.
        /// </summary>
        public string ToRowCssClass() => status switch
        {
            FixedScheduleRowStatus.Expired => "table-danger clsGridRow",
            FixedScheduleRowStatus.Noticed => "table-info clsGridRow",
            _ => "table-inactive clsGridRow"
        };
    }

    extension(FixedIncomeResponse item)
    {
        /// <summary>Maps a single <see cref="FixedIncomeResponse"/> to a <see cref="FixedIncomeOutputViewModel"/> for display.</summary>
        private FixedIncomeOutputViewModel ToDisplayViewModel(
            IReadOnlyDictionary<string, string?> currencyByProduct,
            Func<string, string> localize,
            string timeZoneIanaId,
            DateTime today,
            int noticeWindowDays) => new()
            {
                Id = item.Id,
                MainClass = localize(item.MainClass ?? ""),
                SubClass = localize(item.SubClass ?? ""),
                Content = item.Content,
                Amount = item.Amount,
                MonetaryUnit = currencyByProduct.GetValueOrDefault(item.DepositMyAssetProductName ?? ""),
                DepositMonth = item.DepositMonth,
                DepositDay = item.DepositDay,
                MaturityDate = item.MaturityDate.ToString("yyyy-MM-dd"),
                Note = item.Note,
                DepositMyAssetProductName = item.DepositMyAssetProductName,
                Created = item.Created.ConvertTimeByTimeZoneIanaId(timeZoneIanaId),
                Updated = item.Updated.ConvertTimeByTimeZoneIanaId(timeZoneIanaId),
                Noticed = FixedSchedulePolicy.IsNoticed(item.DepositMonth, item.DepositDay, today, noticeWindowDays, item.Unpunctuality),
                Expired = FixedSchedulePolicy.IsExpired(item.MaturityDate, today),
                Unpunctuality = item.Unpunctuality
            };
    }

    extension(IEnumerable<FixedIncomeResponse> items)
    {
        /// <summary>
        /// Maps all <see cref="FixedIncomeResponse"/> items to display view models; their notice / expiry flags are judged
        /// against <paramref name="utcNow"/> (the request's clock reading) as a date in <paramref name="timeZoneIanaId"/>.
        /// </summary>
        public List<FixedIncomeOutputViewModel> ToDisplayViewModels(
            IEnumerable<AssetResponse> assets,
            Func<string, string> localize,
            string timeZoneIanaId,
            int noticeWindowDays,
            DateTime utcNow)
        {
            var currencyByProduct = assets
                .GroupBy(a => a.ProductName ?? "")
                .ToDictionary(g => g.Key, g => g.Last().MonetaryUnit);
            var today = utcNow.ConvertTimeByTimeZoneIanaId(timeZoneIanaId).Date;
            return
            [
                .. items.Select(i => i.ToDisplayViewModel(currencyByProduct, localize, timeZoneIanaId, today, noticeWindowDays))
            ];
        }
    }

    extension(FixedExpenditureResponse item)
    {
        /// <summary>Maps a single <see cref="FixedExpenditureResponse"/> to a <see cref="FixedExpenditureOutputViewModel"/> for display.</summary>
        private FixedExpenditureOutputViewModel ToDisplayViewModel(
            IReadOnlyDictionary<string, string?> currencyByProduct,
            Func<string, string> localize,
            string timeZoneIanaId,
            DateTime today,
            int noticeWindowDays) => new()
            {
                Id = item.Id,
                MainClass = localize(item.MainClass ?? ""),
                SubClass = localize(item.SubClass ?? ""),
                Content = item.Content,
                Amount = item.Amount,
                MonetaryUnit = currencyByProduct.GetValueOrDefault(item.PaymentMethod ?? ""),
                PaymentMethod = item.PaymentMethod,
                MyDepositAsset = item.MyDepositAsset,
                DepositMonth = item.DepositMonth,
                DepositDay = item.DepositDay,
                MaturityDate = item.MaturityDate.ToString("yyyy-MM-dd"),
                Note = item.Note,
                Created = item.Created.ConvertTimeByTimeZoneIanaId(timeZoneIanaId),
                Updated = item.Updated.ConvertTimeByTimeZoneIanaId(timeZoneIanaId),
                Noticed = FixedSchedulePolicy.IsNoticed(item.DepositMonth, item.DepositDay, today, noticeWindowDays, item.Unpunctuality),
                Expired = FixedSchedulePolicy.IsExpired(item.MaturityDate, today),
                Unpunctuality = item.Unpunctuality
            };
    }

    extension(IEnumerable<FixedExpenditureResponse> items)
    {
        /// <summary>
        /// Maps all <see cref="FixedExpenditureResponse"/> items to display view models; their notice / expiry flags are judged
        /// against <paramref name="utcNow"/> (the request's clock reading) as a date in <paramref name="timeZoneIanaId"/>.
        /// </summary>
        public List<FixedExpenditureOutputViewModel> ToDisplayViewModels(
            IEnumerable<AssetResponse> assets,
            Func<string, string> localize,
            string timeZoneIanaId,
            int noticeWindowDays,
            DateTime utcNow)
        {
            var currencyByProduct = assets
                .GroupBy(a => a.ProductName ?? "")
                .ToDictionary(g => g.Key, g => g.Last().MonetaryUnit);
            var today = utcNow.ConvertTimeByTimeZoneIanaId(timeZoneIanaId).Date;
            return
            [
                .. items.Select(i => i.ToDisplayViewModel(currencyByProduct, localize, timeZoneIanaId, today, noticeWindowDays))
            ];
        }
    }
}
