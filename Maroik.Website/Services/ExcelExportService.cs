using System.Globalization;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Maroik.Core.Contract.Dtos;
using Maroik.Core.Domain.Time;
using Maroik.Website.Contracts;
using Maroik.Website.Extensions;
using Maroik.Website.Mappings;
using Maroik.Website.Models.ViewModels.AccountBook;
using Maroik.Website.Models.ViewModels.Management;
using Maroik.Website.Models.ViewModels.Notice;

namespace Maroik.Website.Services;

/// <summary>
/// Builds OpenXML Excel workbooks for the grids' Excel export (account book, notices, accounts, menu).
/// Localization comes from the caller's <see cref="Func{T,TResult}"/> and timestamps are converted to the
/// caller-supplied time zone, so this class holds no localizer or session state of its own. The fixed
/// income / expenditure exports judge their notice and expiry flags against <paramref name="timeProvider"/>.
/// </summary>
public class ExcelExportService(TimeProvider timeProvider) : IExcelExportService
{
    /// <inheritdoc />
    public MemoryStream CreateAssetExcel(
        IEnumerable<AssetResponse> items,
        Func<string, string> localize,
        string timeZoneIanaId)
    {
        var ordered = items.OrderBy(x => x.ProductName).ToList();

        var headers = new[]
        {
            localize(nameof(AssetOutputViewModel.ProductName)),
            localize(nameof(AssetOutputViewModel.Item)),
            localize(nameof(AssetOutputViewModel.Amount)),
            localize(nameof(AssetOutputViewModel.MonetaryUnit)),
            localize(nameof(AssetOutputViewModel.Created)),
            localize(nameof(AssetOutputViewModel.Updated)),
            localize(nameof(AssetOutputViewModel.Note)),
            localize(nameof(AssetOutputViewModel.Deleted))
        };

        var rows = ordered.Select(item => (IEnumerable<XlsxCell>)
        [
            item.ProductName ?? "",
            localize(item.Item ?? ""),
            XlsxCell.Number(item.Amount),
            item.MonetaryUnit ?? "",
            Timestamp(item.Created.ConvertTimeByTimeZoneIanaId(timeZoneIanaId)),
            Timestamp(item.Updated.ConvertTimeByTimeZoneIanaId(timeZoneIanaId)),
            item.Note ?? "",
            item.Deleted.ToString()
        ]);

        return BuildExcel(headers, rows);
    }

    /// <inheritdoc />
    public MemoryStream CreateIncomeExcel(
        IEnumerable<IncomeResponse> items,
        IEnumerable<AssetResponse> assets,
        Func<string, string> localize,
        string timeZoneIanaId)
    {
        var currencyByProduct = assets
            .GroupBy(a => a.ProductName ?? "")
            .ToDictionary(g => g.Key, g => g.Last().MonetaryUnit ?? "");
        var ordered = items.OrderByDescending(x => x.Created).ThenByDescending(x => x.Updated).ToList();

        var headers = new[]
        {
            localize(nameof(IncomeOutputViewModel.MainClass)),
            localize(nameof(IncomeOutputViewModel.SubClass)),
            localize(nameof(IncomeOutputViewModel.Content)),
            localize(nameof(IncomeOutputViewModel.Amount)),
            localize(nameof(IncomeOutputViewModel.MonetaryUnit)),
            localize(nameof(IncomeOutputViewModel.DepositMyAssetProductName)),
            localize(nameof(IncomeOutputViewModel.Created)),
            localize(nameof(IncomeOutputViewModel.Updated)),
            localize(nameof(IncomeOutputViewModel.Note))
        };

        var rows = ordered.Select(item => (IEnumerable<XlsxCell>)
        [
            localize(item.MainClass ?? ""),
            localize(item.SubClass ?? ""),
            item.Content ?? "",
            XlsxCell.Number(item.Amount),
            currencyByProduct.GetValueOrDefault(item.DepositMyAssetProductName ?? "", ""),
            item.DepositMyAssetProductName ?? "",
            Timestamp(item.Created.ConvertTimeByTimeZoneIanaId(timeZoneIanaId)),
            Timestamp(item.Updated.ConvertTimeByTimeZoneIanaId(timeZoneIanaId)),
            item.Note ?? ""
        ]);

        return BuildExcel(headers, rows);
    }

    /// <inheritdoc />
    public MemoryStream CreateExpenditureExcel(
        IEnumerable<ExpenditureResponse> items,
        IEnumerable<AssetResponse> assets,
        Func<string, string> localize,
        string timeZoneIanaId)
    {
        var currencyByProduct = assets
            .GroupBy(a => a.ProductName ?? "")
            .ToDictionary(g => g.Key, g => g.Last().MonetaryUnit ?? "");
        var ordered = items.OrderByDescending(x => x.Created).ThenByDescending(x => x.Updated).ToList();

        var headers = new[]
        {
            localize(nameof(ExpenditureOutputViewModel.MainClass)),
            localize(nameof(ExpenditureOutputViewModel.SubClass)),
            localize(nameof(ExpenditureOutputViewModel.Content)),
            localize(nameof(ExpenditureOutputViewModel.Amount)),
            localize(nameof(ExpenditureOutputViewModel.MonetaryUnit)),
            localize(nameof(ExpenditureOutputViewModel.PaymentMethod)),
            localize(nameof(ExpenditureOutputViewModel.Note)),
            localize(nameof(ExpenditureOutputViewModel.MyDepositAsset)),
            localize(nameof(ExpenditureOutputViewModel.Created)),
            localize(nameof(ExpenditureOutputViewModel.Updated))
        };

        var rows = ordered.Select(item => (IEnumerable<XlsxCell>)
        [
            localize(item.MainClass ?? ""),
            localize(item.SubClass ?? ""),
            item.Content ?? "",
            XlsxCell.Number(item.Amount),
            currencyByProduct.GetValueOrDefault(item.PaymentMethod ?? "", ""),
            item.PaymentMethod ?? "",
            item.Note ?? "",
            item.MyDepositAsset ?? "",
            Timestamp(item.Created.ConvertTimeByTimeZoneIanaId(timeZoneIanaId)),
            Timestamp(item.Updated.ConvertTimeByTimeZoneIanaId(timeZoneIanaId))
        ]);

        return BuildExcel(headers, rows);
    }

    /// <inheritdoc />
    public MemoryStream CreateFixedIncomeExcel(
        IEnumerable<FixedIncomeResponse> items,
        IEnumerable<AssetResponse> assets,
        Func<string, string> localize,
        string timeZoneIanaId,
        int noticeWindowDays)
    {
        var assetList = assets.ToList();
        var ordered = items
            .ToDisplayViewModels(assetList, localize, timeZoneIanaId, noticeWindowDays, timeProvider.GetUtcNow().UtcDateTime)
            .OrderByDescending(a => a.Expired).ThenByDescending(a => a.Noticed)
            .ThenByDescending(a => a.Unpunctuality).ThenByDescending(a => a.Created)
            .ThenByDescending(a => a.Updated).ToList();

        var headers = new[]
        {
            localize(nameof(FixedIncomeOutputViewModel.MainClass)),
            localize(nameof(FixedIncomeOutputViewModel.SubClass)),
            localize(nameof(FixedIncomeOutputViewModel.Content)),
            localize(nameof(FixedIncomeOutputViewModel.Amount)),
            localize(nameof(FixedIncomeOutputViewModel.MonetaryUnit)),
            localize(nameof(FixedIncomeOutputViewModel.DepositMonth)),
            localize(nameof(FixedIncomeOutputViewModel.DepositDay)),
            localize(nameof(FixedIncomeOutputViewModel.MaturityDate)),
            localize(nameof(FixedIncomeOutputViewModel.Note)),
            localize(nameof(FixedIncomeOutputViewModel.DepositMyAssetProductName)),
            localize(nameof(FixedIncomeOutputViewModel.Created)),
            localize(nameof(FixedIncomeOutputViewModel.Updated)),
            localize(nameof(FixedIncomeOutputViewModel.Noticed)),
            localize(nameof(FixedIncomeOutputViewModel.Expired))
        };

        var rows = ordered.Select(item => (IEnumerable<XlsxCell>)
        [
            item.MainClass ?? "",
            item.SubClass ?? "",
            item.Content ?? "",
            XlsxCell.Number(item.Amount),
            item.MonetaryUnit ?? "",
            item.DepositMonth.ToString(),
            item.DepositDay.ToString(),
            item.MaturityDate ?? "",
            item.Note ?? "",
            item.DepositMyAssetProductName ?? "",
            Timestamp(item.Created),
            Timestamp(item.Updated),
            item.Noticed.ToString(),
            item.Expired.ToString()
        ]);

        return BuildExcel(headers, rows);
    }

    /// <inheritdoc />
    public MemoryStream CreateFixedExpenditureExcel(
        IEnumerable<FixedExpenditureResponse> items,
        IEnumerable<AssetResponse> assets,
        Func<string, string> localize,
        string timeZoneIanaId,
        int noticeWindowDays)
    {
        var assetList = assets.ToList();
        var ordered = items
            .ToDisplayViewModels(assetList, localize, timeZoneIanaId, noticeWindowDays, timeProvider.GetUtcNow().UtcDateTime)
            .OrderByDescending(a => a.Expired).ThenByDescending(a => a.Noticed)
            .ThenByDescending(a => a.Unpunctuality).ThenByDescending(a => a.Created)
            .ThenByDescending(a => a.Updated).ToList();

        var headers = new[]
        {
            localize(nameof(FixedExpenditureOutputViewModel.MainClass)),
            localize(nameof(FixedExpenditureOutputViewModel.SubClass)),
            localize(nameof(FixedExpenditureOutputViewModel.Content)),
            localize(nameof(FixedExpenditureOutputViewModel.Amount)),
            localize(nameof(FixedExpenditureOutputViewModel.MonetaryUnit)),
            localize(nameof(FixedExpenditureOutputViewModel.PaymentMethod)),
            localize(nameof(FixedExpenditureOutputViewModel.MyDepositAsset)),
            localize(nameof(FixedExpenditureOutputViewModel.DepositMonth)),
            localize(nameof(FixedExpenditureOutputViewModel.DepositDay)),
            localize(nameof(FixedExpenditureOutputViewModel.MaturityDate)),
            localize(nameof(FixedExpenditureOutputViewModel.Created)),
            localize(nameof(FixedExpenditureOutputViewModel.Updated)),
            localize(nameof(FixedExpenditureOutputViewModel.Note)),
            localize(nameof(FixedExpenditureOutputViewModel.Noticed)),
            localize(nameof(FixedExpenditureOutputViewModel.Expired))
        };

        var rows = ordered.Select(item => (IEnumerable<XlsxCell>)
        [
            item.MainClass ?? "",
            item.SubClass ?? "",
            item.Content ?? "",
            XlsxCell.Number(item.Amount),
            item.MonetaryUnit ?? "",
            item.PaymentMethod ?? "",
            item.MyDepositAsset ?? "",
            item.DepositMonth.ToString(),
            item.DepositDay.ToString(),
            item.MaturityDate ?? "",
            Timestamp(item.Created),
            Timestamp(item.Updated),
            item.Note ?? "",
            item.Noticed.ToString(),
            item.Expired.ToString()
        ]);

        return BuildExcel(headers, rows);
    }

    /// <inheritdoc />
    public MemoryStream CreateAccountExcel(
        IEnumerable<AccountResponse> items,
        Func<string, string> localize,
        string timeZoneIanaId)
    {
        var ordered = items
            .ToDisplayViewModels(timeZoneIanaId)
            .OrderBy(m => m.Email).ThenBy(m => m.Nickname)
            .ThenByDescending(m => m.Created).ThenByDescending(m => m.Updated)
            .ToList();

        var headers = new[]
        {
            localize(nameof(AccountOutputViewModel.Email)),
            localize(nameof(AccountOutputViewModel.Nickname)),
            localize(nameof(AccountOutputViewModel.AvatarImagePath)),
            localize(nameof(AccountOutputViewModel.Role)),
            localize(nameof(AccountOutputViewModel.TimeZoneIanaId)),
            localize(nameof(AccountOutputViewModel.Locked)),
            localize(nameof(AccountOutputViewModel.LoginAttempt)),
            localize(nameof(AccountOutputViewModel.EmailConfirmed)),
            localize(nameof(AccountOutputViewModel.AgreedServiceTerms)),
            localize(nameof(AccountOutputViewModel.Created)),
            localize(nameof(AccountOutputViewModel.Updated)),
            localize(nameof(AccountOutputViewModel.Message)),
            localize(nameof(AccountOutputViewModel.Deleted))
        };

        var rows = ordered.Select(item => (IEnumerable<XlsxCell>)
        [
            item.Email ?? "",
            item.Nickname ?? "",
            item.AvatarImagePath ?? "",
            item.Role ?? "",
            item.TimeZoneIanaId ?? "",
            item.Locked.ToString(),
            item.LoginAttempt.ToString(),
            item.EmailConfirmed.ToString(),
            item.AgreedServiceTerms.ToString(),
            Timestamp(item.Created),
            Timestamp(item.Updated),
            item.Message ?? "",
            item.Deleted.ToString()
        ]);

        return BuildExcel(headers, rows);
    }

    /// <inheritdoc />
    public MemoryStream CreateMenuExcel(
        IEnumerable<CategoryResponse> categories,
        IEnumerable<SubCategoryResponse> subCategories,
        Func<string, string> localize)
    {
        var rows = categories
            .Select(c => (IEnumerable<XlsxCell>)
            [
                c.Id.ToString(), "", c.Name ?? "", c.DisplayName ?? "",
                c.IconPath ?? "", c.Controller ?? "", c.Action ?? "", c.Role ?? "", c.Order.ToString()
            ])
            .Concat(subCategories.Select(s => (IEnumerable<XlsxCell>)
            [
                s.Id.ToString(), s.CategoryId.ToString(), s.Name ?? "", s.DisplayName ?? "",
                s.IconPath ?? "", "", s.Action ?? "", s.Role ?? "", s.Order.ToString()
            ]))
            .OrderByDescending(r => long.Parse(r.First().Text));

        var headers = new[]
        {
            localize(nameof(MenuOutputViewModel.Id)),
            localize(nameof(MenuOutputViewModel.CategoryId)),
            localize(nameof(MenuOutputViewModel.Name)),
            localize(nameof(MenuOutputViewModel.DisplayName)),
            localize(nameof(MenuOutputViewModel.IconPath)),
            localize(nameof(MenuOutputViewModel.Controller)),
            localize(nameof(MenuOutputViewModel.Action)),
            localize(nameof(MenuOutputViewModel.Role)),
            localize(nameof(MenuOutputViewModel.Order))
        };

        return BuildExcel(headers, rows);
    }

    /// <summary>
    /// Writes a single-sheet ("Sheet1") workbook: one header row, then one row per entry of
    /// <paramref name="rows"/>, each cell as a number or a string as the <see cref="XlsxCell"/> says. Returns the stream rewound to position 0.
    /// </summary>
    private static MemoryStream BuildExcel(IEnumerable<string> headers, IEnumerable<IEnumerable<XlsxCell>> rows)
    {
        var stream = new MemoryStream();

        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();
            var sheets = workbookPart.Workbook.AppendChild(new Sheets());

            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            var sheetData = new SheetData();
            worksheetPart.Worksheet = new Worksheet(sheetData);

            sheets.Append(new Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1,
                Name = "Sheet1"
            });

            var headerRow = new Row();
            foreach (string header in headers)
                headerRow.Append(StringCell(header));
            sheetData.Append(headerRow);

            foreach (var rowValues in rows)
            {
                var dataRow = new Row();
                foreach (XlsxCell value in rowValues)
                    dataRow.Append(value.IsNumber ? NumberCell(value.Text) : StringCell(value.Text));
                sheetData.Append(dataRow);
            }

            workbookPart.Workbook.Save();
        }

        stream.Position = 0;
        return stream;
    }

    /// <summary>A number-typed cell holding the invariant-culture <paramref name="value"/>, so Excel can sum and sort it.</summary>
    private static Cell NumberCell(string value) => new()
    {
        CellValue = new CellValue(value),
        DataType = CellValues.Number
    };

    /// <summary>The one text format every exported timestamp uses, whatever the server or viewer culture.</summary>
    private static string Timestamp(DateTime value) => value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    /// <summary>
    /// One exported cell: text, or a number written in invariant-culture notation (amounts — with every
    /// stored decimal kept, so the sheet totals exactly what the account book does; an amount Excel cannot hold
    /// exactly stays text).
    /// </summary>
    private readonly record struct XlsxCell(string Text, bool IsNumber)
    {
        /// <summary>A text cell.</summary>
        public static implicit operator XlsxCell(string text) => new(text, false);

        /// <summary>
        /// A numeric cell holding <paramref name="value"/>, or a text cell holding it exactly when it has more
        /// significant digits than Excel keeps for a number (<see cref="ExcelSignificantDigits"/>): Excel would
        /// otherwise round it silently.
        /// </summary>
        public static XlsxCell Number(decimal value)
        {
            string text = value.TrimTrailingZeros();
            return new(text, SignificantDigits(text) <= ExcelSignificantDigits);
        }

        /// <summary>Excel stores a number as a double and keeps 15 significant digits of it.</summary>
        private const int ExcelSignificantDigits = 15;

        /// <summary>Digits of an invariant-culture decimal <paramref name="text"/>, leading and trailing zeros not counted.</summary>
        private static int SignificantDigits(string text) =>
            text.Where(char.IsAsciiDigit).SkipWhile(d => d == '0').Reverse().SkipWhile(d => d == '0').Count();
    }

    /// <summary>A string-typed cell holding <paramref name="value"/> with XML-illegal characters removed.</summary>
    private static Cell StringCell(string value) => new()
    {
        CellValue = new CellValue(StripXmlInvalidChars(value)),
        DataType = CellValues.String
    };

    /// <summary>
    /// Removes characters that are illegal in XML 1.0: C0 control characters other than tab/CR/LF,
    /// U+FFFE/U+FFFF, and <em>unpaired</em> UTF-16 surrogate code units (a lone high or low
    /// surrogate — e.g. from a truncated emoji or bad clipboard data). User-entered fields such as
    /// Note/Content/Nickname can carry any of these from pasted text; left in, the OpenXml
    /// <see cref="System.Xml.XmlWriter"/> throws during Workbook.Save, breaking the
    /// whole export over one row. Valid surrogate pairs (real astral-plane characters) are kept.
    /// </summary>
    private static string StripXmlInvalidChars(string value)
    {
        if (string.IsNullOrEmpty(value) || !ContainsXmlInvalid(value))
            return value;

        var sb = new StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];

            if (char.IsHighSurrogate(c))
            {
                if (i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
                {
                    sb.Append(c);
                    sb.Append(value[i + 1]);
                    i++;
                }
                // else: drop the unpaired high surrogate
                continue;
            }

            if (char.IsLowSurrogate(c))
                continue; // unpaired low surrogate

            if (!IsXmlInvalid(c))
                sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>True when <paramref name="value"/> holds any char <see cref="StripXmlInvalidChars"/> would remove.</summary>
    private static bool ContainsXmlInvalid(string value)
    {
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (char.IsHighSurrogate(c))
            {
                if (i + 1 >= value.Length || !char.IsLowSurrogate(value[i + 1]))
                {
                    return true; // unpaired high surrogate
                }
                i++; continue;
            }
            if (char.IsLowSurrogate(c) || IsXmlInvalid(c))
                return true;
        }
        return false;
    }

    /// <summary>True for a C0 control character other than tab / CR / LF, or the non-characters U+FFFE / U+FFFF.</summary>
    private static bool IsXmlInvalid(char c) =>
        (c < 0x20 && c != '\t' && c != '\n' && c != '\r') || c == '\uFFFE' || c == '\uFFFF';
}
