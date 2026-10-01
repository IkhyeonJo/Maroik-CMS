using Maroik.Core.Contract.Dtos;
using Maroik.Core.Contract.Interfaces;
using Maroik.Core.Contract.Misc.Settings;
using Maroik.Core.Domain.Account;
using Maroik.Core.Domain.Finance;
using Maroik.Core.Domain.Localization;
using Maroik.Core.Domain.ValueObjects;
using Maroik.Core.Service.Mappers;
using Microsoft.Extensions.Options;

namespace Maroik.Core.Service.Services;

/// <summary>
/// Implementation of <see cref="IDashboardService"/> that aggregates data from multiple repositories
/// to populate the user dashboard and notification badge counts.
/// Calculates yearly/monthly income and expenditure totals grouped by MainClass/SubClass,
/// and computes notification counts for fixed-income and fixed-expenditure entries
/// whose deposit dates are approaching or have passed maturity.
/// </summary>
public class DashboardService(
    IAccountRepository accountRepository,
    IAssetRepository assetRepository,
    IIncomeRepository incomeRepository,
    IExpenditureRepository expenditureRepository,
    IFixedIncomeRepository fixedIncomeRepository,
    IFixedExpenditureRepository fixedExpenditureRepository,
    IOptions<ServerSetting> settings,
    TimeProvider timeProvider) : IDashboardService
{
    /// <inheritdoc />
    public async Task<DashboardDto> GetSummaryAsync(string accountEmail, string year, string month, string timeZoneId, CancellationToken ct = default)
    {
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;

        // The public demo account always shows a fixed, pre-seeded period (configured via
        // ServerSetting.DemoAccountEmail/DemoDashboardYear/DemoDashboardMonth) so visitors see
        // consistent sample data regardless of when they browse the demo.
        bool isDemoAccount = !string.IsNullOrEmpty(settings.Value.DemoAccountEmail) && accountEmail == settings.Value.DemoAccountEmail;
        if (isDemoAccount)
        {
            year = settings.Value.DemoDashboardYear ?? year;
            month = settings.Value.DemoDashboardMonth ?? month;
        }

        // Normalize year/month
        DateTime accountToday = utcNow.ConvertTimeByTimeZoneIanaId(timeZoneId);
        if (!int.TryParse(year, out int yearInt)) yearInt = accountToday.Year;
        if (!int.TryParse(month, out int monthInt)) monthInt = accountToday.Month;

        // An out-of-range month (or year outside DateTime's valid range) is replaced here with the
        // account's current one, so the `new DateTime(yearInt, monthInt, 1)` calls below can never throw
        // on a hand-edited query string.
        if (monthInt is < 1 or > 12) monthInt = accountToday.Month;
        // Capped one below DateTime.MaxValue's year: yearEnd below computes `new DateTime(yearInt + 1, ...)`.
        if (yearInt is < 1 or > 9998) yearInt = accountToday.Year;

        // main computes DefaultMonetaryUnit/MonetaryUnits from the account's full asset list,
        // including deleted ones (AssetRepository.GetAssetsAsync never filters Deleted) — so a
        // user whose only assets happen to be deleted still resolves the same effective currency
        // and sees the same MonetaryUnits options main would show. Only the visible asset card
        // list (`assets` below) is deleted-filtered, since main has no such list to match.
        List<Asset> allAssets = await assetRepository.GetByAccountEmailAsync(accountEmail, ct);
        List<Asset> assets = [.. allAssets.Where(a => !a.Deleted)];
        Account? account = await accountRepository.FindByEmailAsync(accountEmail, ct);

        CurrencyCode? defaultMonetaryUnit = account == null ? null : DefaultMonetaryUnitPolicy.Resolve(account.DefaultMonetaryUnit, allAssets);

        // Persist the self-corrected value (matches main): other screens that read
        // Account.DefaultMonetaryUnit directly (e.g. account management) must not see a
        // stale/empty value while the dashboard itself already shows the resolved one.
        // Never do this for the public demo account: its data is pre-seeded and fixed, and it is
        // browsed anonymously, so write here would fire on ordinary anonymous page views.
        if (!isDemoAccount && account != null && account.DefaultMonetaryUnit != defaultMonetaryUnit)
        {
            // Column-scoped write (DefaultMonetaryUnit only, no Updated bump): this fires on a
            // dashboard GET, so a full-row UpdateEntityAsync here would race — and silently
            // overwrite — a concurrent profile edit on the same account.
            await accountRepository.UpdateDefaultMonetaryUnitAsync(accountEmail, defaultMonetaryUnit?.Value, ct);
        }

        var summary = new DashboardDto
        {
            DefaultMonetaryUnit = defaultMonetaryUnit?.Value,
            Assets = assets.Select(AssetMapper.ToResponse).ToList(),
            SelectedYear = yearInt,
            SelectedMonth = monthInt,
            EndYear = accountToday.Year,
            MonetaryUnits = allAssets.Select(x => x.Balance.Currency.Value).Distinct()
        };

        if (summary.DefaultMonetaryUnit != null)
        {
            DateTime yearStart = new DateTime(yearInt, 1, 1).ConvertToUtcByTimeZoneIanaId(timeZoneId);
            DateTime yearEnd = new DateTime(yearInt + 1, 1, 1).ConvertToUtcByTimeZoneIanaId(timeZoneId);
            DateTime ymStart = new DateTime(yearInt, monthInt, 1).ConvertToUtcByTimeZoneIanaId(timeZoneId);
            DateTime ymEnd = new DateTime(yearInt, monthInt, 1).AddMonths(1).ConvertToUtcByTimeZoneIanaId(timeZoneId);

            List<Income> yearIncomes = await incomeRepository.GetByAccountEmailAndDateRangeAsync(accountEmail, yearStart, yearEnd, ct);
            List<Expenditure> yearExpenditures = await expenditureRepository.GetByAccountEmailAndDateRangeAsync(accountEmail, yearStart, yearEnd, ct);

            // ymStart/ymEnd always fall within [yearStart, yearEnd), so the year-month totals can
            // be derived from the year totals already fetched above instead of two more DB round
            // trips (repositories share a single scoped DbContext, so these calls cannot run
            // concurrently via Task.WhenAll either).
            List<Income> ymIncomes = [.. yearIncomes.Where(x => x.Created >= ymStart && x.Created < ymEnd)];
            List<Expenditure> ymExpenditures = [.. yearExpenditures.Where(x => x.Created >= ymStart && x.Created < ymEnd)];

            // Filter by default monetary unit via a currency-by-product lookup built once. Uses
            // allAssets (not the deleted-filtered `assets`) so a transaction tied to a
            // since-deleted asset still resolves its currency correctly instead of being dropped
            // from the totals.
            string defaultUnit = summary.DefaultMonetaryUnit;
            Dictionary<string, string> currencyByProduct = allAssets
                .GroupBy(a => a.ProductName)
                .ToDictionary(g => g.Key, g => g.Last().Balance.Currency.Value);
            summary.CurrencyByProduct = currencyByProduct;

            summary.YearIncomes =
            [
                .. yearIncomes
                    .Where(x => currencyByProduct.GetValueOrDefault(x.DepositMyAssetProductName) == defaultUnit)
                    .Select(IncomeMapper.ToResponse)
            ];
            summary.YearExpenditures =
            [
                .. yearExpenditures
                    .Where(x => currencyByProduct.GetValueOrDefault(x.PaymentMethod) == defaultUnit)
                    .Select(ExpenditureMapper.ToResponse)
            ];
            summary.YearMonthIncomes =
            [
                .. ymIncomes
                    .Where(x => currencyByProduct.GetValueOrDefault(x.DepositMyAssetProductName) == defaultUnit)
                    .Select(IncomeMapper.ToResponse)
            ];
            summary.YearMonthExpenditures =
            [
                .. ymExpenditures
                    .Where(x => currencyByProduct.GetValueOrDefault(x.PaymentMethod) == defaultUnit)
                    .Select(ExpenditureMapper.ToResponse)
            ];

            // Per-main-class subClass amount/percentage breakdowns (labor/business/pension income,
            // meal/housing/education spending, ...) the dashboard view displays. Computed here, from
            // IncomeClassPolicy/ExpenditureClassPolicy's own main-class list, so the view model
            // mapper only reads pre-computed numbers instead of re-deriving these sums itself.
            summary.YearIncomeBreakdown = BuildBreakdown(
                summary.YearIncomes.Select(i => (i.MainClass, i.SubClass, i.Amount)), IncomeClassPolicy.SubClassesByMainClass.Keys);
            summary.YearMonthIncomeBreakdown = BuildBreakdown(
                summary.YearMonthIncomes.Select(i => (i.MainClass, i.SubClass, i.Amount)), IncomeClassPolicy.SubClassesByMainClass.Keys);
            summary.YearExpenditureBreakdown = BuildBreakdown(
                summary.YearExpenditures.Select(e => (e.MainClass, e.SubClass, e.Amount)), ExpenditureClassPolicy.SubClassesByMainClass.Keys);
            summary.YearMonthExpenditureBreakdown = BuildBreakdown(
                summary.YearMonthExpenditures.Select(e => (e.MainClass, e.SubClass, e.Amount)), ExpenditureClassPolicy.SubClassesByMainClass.Keys);
        }

        // Compute StartYear
        var firstIncome = await incomeRepository.GetFirstByAccountEmailOrderedByCreatedAsync(accountEmail, ct);
        var firstExpenditure = await expenditureRepository.GetFirstByAccountEmailOrderedByCreatedAsync(accountEmail, ct);

        DateTime firstCreated = DateTime.MaxValue;
        if (firstIncome != null && firstIncome.Created != DateTime.MinValue && firstIncome.Created < firstCreated)
            firstCreated = firstIncome.Created;
        if (firstExpenditure != null && firstExpenditure.Created != DateTime.MinValue && firstExpenditure.Created < firstCreated)
            firstCreated = firstExpenditure.Created;
        if (firstCreated == DateTime.MaxValue)
            firstCreated = utcNow;

        // Convert to user timezone. ConvertTimeByTimeZoneIanaId (not the raw TimeZoneInfo API) is
        // required here: firstCreated is DateTimeKind.Unspecified as returned by Npgsql but
        // actually represents a UTC instant, and the raw API infers its source zone from Kind,
        // silently falling back to the OS-local zone instead of UTC for Unspecified values.
        firstCreated = firstCreated.ConvertTimeByTimeZoneIanaId(timeZoneId);

        summary.StartYear = firstCreated.Year;

        return summary;
    }

    /// <inheritdoc />
    public async Task<NotificationDto> GetNoticeCountsAsync(string email, int noticeMaturityDateDay, string timeZoneId, CancellationToken ct = default)
    {
        int incomesNoticed = 0, expendituresNoticed = 0;
        int incomesExpired = 0, expendituresExpired = 0;

        DateTime currentDate = timeProvider.GetUtcNow().UtcDateTime.ConvertTimeByTimeZoneIanaId(timeZoneId).Date;

        foreach (var item in await fixedIncomeRepository.GetByAccountEmailAsync(email, ct))
        {
            if (FixedSchedulePolicy.IsNoticed(item.DepositMonth, item.DepositDay, currentDate, noticeMaturityDateDay, item.Unpunctuality))
                incomesNoticed++;
            if (FixedSchedulePolicy.IsExpired(item.MaturityDate, currentDate))
                incomesExpired++;
        }

        foreach (var item in await fixedExpenditureRepository.GetByAccountEmailAsync(email, ct))
        {
            if (FixedSchedulePolicy.IsNoticed(item.DepositMonth, item.DepositDay, currentDate, noticeMaturityDateDay, item.Unpunctuality))
                expendituresNoticed++;
            if (FixedSchedulePolicy.IsExpired(item.MaturityDate, currentDate))
                expendituresExpired++;
        }

        return new NotificationDto
        {
            FixedIncomesNoticed = incomesNoticed,
            FixedExpendituresNoticed = expendituresNoticed,
            FixedIncomesExpired = incomesExpired,
            FixedExpendituresExpired = expendituresExpired
        };
    }

    /// <inheritdoc />
    public ServerResourceDto GetServerResourceSummary(string hostResourceFilePath, string dockerResourceFilePath)
    {
        string hostText = File.Exists(hostResourceFilePath) ? File.ReadAllText(hostResourceFilePath) : "";
        string dockerText = File.Exists(dockerResourceFilePath) ? File.ReadAllText(dockerResourceFilePath) : "";

        string hostCpuInfo = ExtractSection(hostText, "Host CPU Information:", "Host Memory Information:");
        string hostMemoryInfo = ExtractSection(hostText, "Host Memory Information:", "Host Disk Information:");
        string hostDiskInfo = ExtractSection(hostText, "Host Disk Information:");
        string dockerResourceUsage = ExtractSection(dockerText, "Docker Resource Usage:");

        var dockerContainerResult = new List<DockerContainerResourceDto>();
        // Plain string splitting of text that is already a non-null string: nothing here can throw, and a malformed
        // line is simply skipped (fewer than five columns).
        var lines = dockerResourceUsage.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var resourceMap = new Dictionary<string, string[]>();
        for (int i = 1; i < lines.Length; i++)
        {
            var cols = lines[i].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (cols.Length < 5) continue;
            resourceMap[cols[0]] = cols;
        }
        for (int i = 1; i < lines.Length; i++)
        {
            var cols = lines[i].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (cols.Length < 5) continue;
            string name = cols[0];
            if (resourceMap.TryGetValue(name, out var r))
            {
                dockerContainerResult.Add(new DockerContainerResourceDto
                {
                    Name = name,
                    CpuPercent = r[1],
                    MemUsageDisplay = $"{r[2]} {r[3]} {r[4]}"
                });
            }
        }

        string memUsageLimit = "";
        if (!string.IsNullOrEmpty(hostMemoryInfo))
        {
            string[] temp = hostMemoryInfo.Replace("Memory: ", "").Split('/');
            if (temp.Length >= 2)
                memUsageLimit = temp[1].Trim() + " / " + temp[0].Trim();
        }

        string diskUsageLimit = "";
        if (!string.IsNullOrEmpty(hostDiskInfo))
        {
            string[] temp = hostDiskInfo.Replace("Disk: ", "").Split('/');
            if (temp.Length >= 2)
                diskUsageLimit = temp[1].Trim() + " / " + temp[0].Trim();
        }

#pragma warning disable CA1806
        double.TryParse(
#pragma warning restore CA1806
#pragma warning disable IDE0305
            new string(hostCpuInfo.Where(c => char.IsDigit(c) || c == '.').ToArray()),
#pragma warning restore IDE0305
            out double hostCpuNumeric);

        return new ServerResourceDto
        {
            HostCpuNumeric = hostCpuNumeric,
            HostCpuInfo = hostCpuInfo,
            MemUsageLimit = memUsageLimit,
            HostMemoryInfo = hostMemoryInfo,
            DiskUsageLimit = diskUsageLimit,
            HostDiskInfo = hostDiskInfo,
            DockerContainerResult = dockerContainerResult
        };
    }

    /// <summary>
    /// Computes a <see cref="FinanceBreakdownDto"/> per main class in <paramref name="mainClasses"/> from
    /// <paramref name="items"/> via <see cref="FinanceBreakdownPolicy"/>. Shared by both the income and
    /// expenditure call sites -- callers project their <c>IncomeResponse</c>/<c>ExpenditureResponse</c>
    /// list to the common <c>(MainClass, SubClass, Amount)</c> shape first. Groups <paramref name="items"/>
    /// by main class once via <see cref="Enumerable.ToLookup{TSource,TKey}(IEnumerable{TSource},Func{TSource,TKey})"/>
    /// so this is a single O(items) pass rather than re-scanning the full list once per main class.
    /// </summary>
    private static Dictionary<string, FinanceBreakdownDto> BuildBreakdown(
        IEnumerable<(string? MainClass, string? SubClass, decimal Amount)> items, IEnumerable<string> mainClasses)
    {
        var byMainClass = items.ToLookup(i => i.MainClass);
        return mainClasses.ToDictionary(
            mainClass => mainClass,
            mainClass => ToDto(FinanceBreakdownPolicy.Compute(byMainClass[mainClass].Select(i => (i.SubClass, i.Amount)))));
    }

    /// <summary>Copies a domain <see cref="FinanceBreakdownPolicy.SubClassBreakdown"/> into its DTO (fresh, mutable dictionaries).</summary>
    private static FinanceBreakdownDto ToDto(FinanceBreakdownPolicy.SubClassBreakdown breakdown) => new()
    {
        Total = breakdown.Total,
        AmountBySubClass = new Dictionary<string, decimal>(breakdown.AmountBySubClass),
        PercentageBySubClass = new Dictionary<string, double>(breakdown.PercentageBySubClass)
    };

    /// <summary>
    /// Extracts and trims the text between <paramref name="startMarker"/> and <paramref name="endMarker"/>
    /// (or to the end of <paramref name="input"/> when <paramref name="endMarker"/> is omitted).
    /// Returns an empty string when a marker is missing (the search itself cannot fail on a non-null string),
    /// so a malformed resource log never breaks the whole summary.
    /// </summary>
    private static string ExtractSection(string input, string startMarker, string? endMarker = null)
    {
        int markerIndex = input.IndexOf(startMarker, StringComparison.Ordinal);
        if (markerIndex < 0) return "";

        int startIndex = markerIndex + startMarker.Length;
        int endIndex = endMarker != null ? input.IndexOf(endMarker, startIndex, StringComparison.Ordinal) : input.Length;
#pragma warning disable IDE0057
        return endIndex < 0 ? "" : input.Substring(startIndex, endIndex - startIndex).Trim();
#pragma warning restore IDE0057
    }
}
