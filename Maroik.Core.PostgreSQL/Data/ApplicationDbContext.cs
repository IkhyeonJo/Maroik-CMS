using System;
using System.Collections.Generic;
using Maroik.Core.PostgreSQL.Models;
using Microsoft.EntityFrameworkCore;

namespace Maroik.Core.PostgreSQL.Data;

/// <summary>
/// EF Core context over the Maroik PostgreSQL schema (scaffolded from the database). The schema itself
/// is created by <c>Maroik.DB/PostgreSQL/SQL_Init_Script/*/Init.sql</c>, not by this model; the mapping
/// below must match it (constraint / index names are relied on by the Service layer's error classification).
/// </summary>
public partial class ApplicationDbContext : DbContext
{
    /// <summary>Creates the context with the options configured at DI registration (Npgsql connection string).</summary>
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    /// <summary>"Account" table.</summary>
    public virtual DbSet<Account> Accounts { get; set; }

    /// <summary>"Asset" table.</summary>
    public virtual DbSet<Asset> Assets { get; set; }

    /// <summary>"Board" table.</summary>
    public virtual DbSet<Board> Boards { get; set; }

    /// <summary>"BoardAttachedFile" table.</summary>
    public virtual DbSet<BoardAttachedFile> BoardAttachedFiles { get; set; }

    /// <summary>"BoardComment" table.</summary>
    public virtual DbSet<BoardComment> BoardComments { get; set; }

    /// <summary>"Calendar" table.</summary>
    public virtual DbSet<Calendar> Calendars { get; set; }

    /// <summary>"CalendarEvent" table.</summary>
    public virtual DbSet<CalendarEvent> CalendarEvents { get; set; }

    /// <summary>"CalendarEventAttachedFile" table.</summary>
    public virtual DbSet<CalendarEventAttachedFile> CalendarEventAttachedFiles { get; set; }

    /// <summary>"CalendarEventReminder" table.</summary>
    public virtual DbSet<CalendarEventReminder> CalendarEventReminders { get; set; }

    /// <summary>"CalendarRecurrence" table.</summary>
    public virtual DbSet<CalendarRecurrence> CalendarRecurrences { get; set; }

    /// <summary>"CalendarShared" table.</summary>
    public virtual DbSet<CalendarShared> CalendarShareds { get; set; }

    /// <summary>"Category" table.</summary>
    public virtual DbSet<Category> Categories { get; set; }

    /// <summary>"Expenditure" table.</summary>
    public virtual DbSet<Expenditure> Expenditures { get; set; }

    /// <summary>"FixedExpenditure" table.</summary>
    public virtual DbSet<FixedExpenditure> FixedExpenditures { get; set; }

    /// <summary>"FixedIncome" table.</summary>
    public virtual DbSet<FixedIncome> FixedIncomes { get; set; }

    /// <summary>"Income" table.</summary>
    public virtual DbSet<Income> Incomes { get; set; }

    /// <summary>"OtherCalendar" table.</summary>
    public virtual DbSet<OtherCalendar> OtherCalendars { get; set; }

    /// <summary>"SubCategory" table.</summary>
    public virtual DbSet<SubCategory> SubCategories { get; set; }

    /// <summary>Maps every entity to its table: keys, constraint / index names, column types, defaults, comments and relationships.</summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasKey(e => e.Email).HasName("Account_pk");

            entity.ToTable("Account", tb => tb.HasComment("Account"));

            entity.HasIndex(e => e.Nickname, "Account_Nickname_unique").IsUnique();
            // Not mapped here on purpose: the case-insensitive UNIQUE index "Account_unique_index_0" on
            // lower("Nickname") lives only in Init.sql. EF Core / Npgsql have no expression-index mapping (a
            // HasIndex on Nickname would misstate it as a plain index on the column). The schema is created
            // from Init.sql, not from this model, so nothing is lost; DbExceptionExtensions matches the index
            // by name when it classifies a duplicate-nickname violation.

            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .HasComment("Email (ID)");
            entity.Property(e => e.AgreedServiceTerms).HasComment("AgreedServiceTerms");
            entity.Property(e => e.AvatarImagePath)
                .HasMaxLength(255)
                .HasDefaultValueSql("'/upload/Management/Profile/default-avatar.jpg'::character varying")
                .HasComment("AvatarImagePath");
            entity.Property(e => e.Created)
                .HasDefaultValueSql("now()")
                .HasComment("Created")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.DefaultMonetaryUnit)
                .HasMaxLength(45)
                .HasDefaultValueSql("NULL::character varying")
                .HasComment("Default monetary unit (KRW, USD, ETC)");
            entity.Property(e => e.Deleted).HasComment("Deleted");
            entity.Property(e => e.EmailConfirmed).HasComment("EmailConfirmed");
            entity.Property(e => e.HashedPassword).HasComment("HashedPassword");
            entity.Property(e => e.Locked).HasComment("Locked");
            entity.Property(e => e.LoginAttempt).HasComment("LoginAttempt");
            entity.Property(e => e.Message).HasComment("Message");
            entity.Property(e => e.MustChangePassword).HasComment("Forces a password change on next login (set by an admin password override)");
            entity.Property(e => e.DeviceStamp)
                .HasDefaultValueSql("(gen_random_uuid())::text")
                .HasComment("Opaque value every trusted-device cookie of the account carries; replacing it untrusts every device");
            entity.Property(e => e.TrustedDeviceLoginAttempt)
                .HasDefaultValueSql("0")
                .HasComment("Consecutive failed logins made from trusted devices");
            entity.Property(e => e.LoginBlockedUntil)
                .HasComment("Until when new logins are held off after failed attempts; NULL when they are not")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.Nickname)
                .HasMaxLength(255)
                .HasComment("Nickname");
            entity.Property(e => e.RegistrationToken).HasComment("RegistrationToken");
            entity.Property(e => e.ResetPasswordToken).HasComment("ResetPasswordToken");
            entity.Property(e => e.Role)
                .HasMaxLength(255)
                .HasDefaultValueSql("'User'::character varying")
                .HasComment("Role (Admin or User)");
            entity.Property(e => e.SecurityStamp)
                .HasDefaultValueSql("(gen_random_uuid())::text")
                .HasComment("Opaque value that changes on every password change; invalidates other open sessions");
            entity.Property(e => e.TimeZoneIanaId)
                .HasMaxLength(255)
                .HasDefaultValueSql("'UTC'::character varying")
                .HasComment("IANA TimeZone ID");
            entity.Property(e => e.Updated)
                .HasDefaultValueSql("now()")
                .HasComment("Updated")
                .HasColumnType("timestamp without time zone");
        });

        modelBuilder.Entity<Asset>(entity =>
        {
            entity.HasKey(e => new { e.ProductName, e.AccountEmail }).HasName("Asset_pk");

            entity.ToTable("Asset", tb => tb.HasComment("Asset"));

            entity.HasIndex(e => e.AccountEmail, "Asset_index_0");

            entity.Property(e => e.ProductName)
                .HasMaxLength(255)
                .HasComment("ProductName");
            entity.Property(e => e.AccountEmail)
                .HasMaxLength(255)
                .HasComment("AccountEmail (ID)");
            entity.Property(e => e.Amount)
                .HasPrecision(20, 4)
                .HasComment("Amount");
            entity.Property(e => e.Created)
                .HasComment("Created")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.Deleted).HasComment("Deleted");
            entity.Property(e => e.Item)
                .HasMaxLength(255)
                .HasComment("Item");
            entity.Property(e => e.MonetaryUnit)
                .HasMaxLength(45)
                .HasComment("MonetaryUnit (KRW, USD, ETC)");
            entity.Property(e => e.Note)
                .HasMaxLength(255)
                .HasDefaultValueSql("''::character varying")
                .HasComment("Note");
            entity.Property(e => e.Updated)
                .HasComment("Updated")
                .HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.AccountEmailNavigation).WithMany(p => p.Assets)
                .HasForeignKey(d => d.AccountEmail)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Asset_fk_0");
        });

        modelBuilder.Entity<Board>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Board_pk");

            entity.ToTable("Board", tb => tb.HasComment("Board"));

            entity.HasIndex(e => new { e.Type, e.Noticed, e.Id }, "Board_index_0");

            entity.Property(e => e.Id).HasComment("PK");
            entity.Property(e => e.Content).HasComment("Content");
            entity.Property(e => e.Created)
                .HasComment("Created")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.Deleted).HasComment("Deleted");
            entity.Property(e => e.Locked).HasComment("Locked");
            entity.Property(e => e.Noticed).HasComment("Noticed");
            entity.Property(e => e.Title)
                .HasMaxLength(255)
                .HasComment("Title");
            entity.Property(e => e.Type)
                .HasMaxLength(255)
                .HasComment("Type");
            entity.Property(e => e.Updated)
                .HasComment("Updated")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.View).HasComment("View");
            entity.Property(e => e.Writer)
                .HasMaxLength(255)
                .HasComment("Writer");

            entity.HasOne(d => d.WriterNavigation).WithMany(p => p.Boards)
                .HasPrincipalKey(p => p.Nickname)
                .HasForeignKey(d => d.Writer)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Board_fk_0");
        });

        modelBuilder.Entity<BoardAttachedFile>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("BoardAttachedFile_pk");

            entity.ToTable("BoardAttachedFile", tb => tb.HasComment("BoardAttachedFile"));

            entity.HasIndex(e => e.BoardId, "BoardAttachedFile_index_0");

            entity.Property(e => e.Id).HasComment("PK");
            entity.Property(e => e.BoardId).HasComment("Parent Board Id");
            entity.Property(e => e.Extension)
                .HasMaxLength(255)
                .HasDefaultValueSql("NULL::character varying")
                .HasComment("Extension");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasComment("Name");
            entity.Property(e => e.Path)
                .HasMaxLength(255)
                .HasComment("Path");
            entity.Property(e => e.Size).HasComment("Size (Byte)");

            entity.HasOne(d => d.Board).WithMany(p => p.BoardAttachedFiles)
                .HasForeignKey(d => d.BoardId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("BoardAttachedFile_fk_0");
        });

        modelBuilder.Entity<BoardComment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("BoardComment_pk");

            entity.ToTable("BoardComment", tb => tb.HasComment("BoardComment"));

            entity.HasIndex(e => e.BoardId, "BoardComment_index_0");

            entity.Property(e => e.Id).HasComment("PK");
            entity.Property(e => e.AvatarImagePath)
                .HasMaxLength(255)
                .HasComment("AvatarImagePath");
            entity.Property(e => e.BoardId).HasComment("Board Id");
            entity.Property(e => e.Content).HasComment("Content");
            entity.Property(e => e.Created)
                .HasComment("Created")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.Deleted).HasComment("Deleted");
            entity.Property(e => e.Order).HasComment("Order");
            entity.Property(e => e.Writer)
                .HasMaxLength(255)
                .HasComment("Writer");

            entity.HasOne(d => d.Board).WithMany(p => p.BoardComments)
                .HasForeignKey(d => d.BoardId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("BoardComment_fk_0");

            entity.HasOne(d => d.WriterNavigation).WithMany(p => p.BoardComments)
                .HasPrincipalKey(p => p.Nickname)
                .HasForeignKey(d => d.Writer)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("BoardComment_fk_1");
        });

        modelBuilder.Entity<Calendar>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Calendar_pk");

            entity.ToTable("Calendar", tb => tb.HasComment("Calendar"));

            entity.HasIndex(e => new { e.AccountEmail, e.Name }, "Calendar_AccountEmail_Name_unique").IsUnique();

            entity.HasIndex(e => e.AccountEmail, "Calendar_index_0");

            entity.Property(e => e.Id).HasComment("PK");
            entity.Property(e => e.AccountEmail)
                .HasMaxLength(255)
                .HasComment("AccountEmail (ID)");
            entity.Property(e => e.Created)
                .HasComment("Created")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.Description).HasComment("Description");
            entity.Property(e => e.HtmlColorCode)
                .HasMaxLength(10)
                .HasComment("HtmlColorCode");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasComment("Name");
            entity.Property(e => e.TimeZoneIanaId)
                .HasMaxLength(255)
                .HasComment("IANA TimeZone ID");
            entity.Property(e => e.Updated)
                .HasComment("Updated")
                .HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.AccountEmailNavigation).WithMany(p => p.Calendars)
                .HasForeignKey(d => d.AccountEmail)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Calendar_fk_0");
        });

        modelBuilder.Entity<CalendarEvent>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("CalendarEvent_pk");

            entity.ToTable("CalendarEvent", tb => tb.HasComment("CalendarEvent"));

            entity.HasIndex(e => e.RecurrenceId, "CalendarEvent_index_1");

            entity.HasIndex(e => new { e.CalendarId, e.StartDate }, "CalendarEvent_index_2");

            entity.Property(e => e.Id).HasComment("PK");
            entity.Property(e => e.AllDay).HasComment("AllDay");
            entity.Property(e => e.CalendarId).HasComment("Parent Calendar Id");
            entity.Property(e => e.Created)
                .HasComment("Created")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.Description).HasComment("Description");
            entity.Property(e => e.EndDate)
                .HasComment("EndDate")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.EndDateTimeZoneIanaId)
                .HasMaxLength(255)
                .HasDefaultValueSql("NULL::character varying")
                .HasComment("EndDateTimeZone (IANA TimeZone ID)");
            entity.Property(e => e.Location)
                .HasMaxLength(255)
                .HasDefaultValueSql("NULL::character varying")
                .HasComment("Location");
            entity.Property(e => e.RecurrenceId).HasComment("Recurrence ID (Option)");
            entity.Property(e => e.StartDate)
                .HasComment("StartDate")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.StartDateTimeZoneIanaId)
                .HasMaxLength(255)
                .HasDefaultValueSql("NULL::character varying")
                .HasComment("StartDateTimeZone (IANA TimeZone ID)");
            entity.Property(e => e.Status)
                .HasMaxLength(255)
                .HasDefaultValueSql("'Busy'::character varying")
                .HasComment("Status");
            entity.Property(e => e.Title)
                .HasMaxLength(255)
                .HasComment("Title");
            entity.Property(e => e.Updated)
                .HasComment("Updated")
                .HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.Calendar).WithMany(p => p.CalendarEvents)
                .HasForeignKey(d => d.CalendarId)
                .HasConstraintName("CalendarEvent_fk_0");

            entity.HasOne(d => d.Recurrence).WithMany(p => p.CalendarEvents)
                .HasForeignKey(d => d.RecurrenceId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("CalendarEvent_fk_1");
        });

        modelBuilder.Entity<CalendarEventAttachedFile>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("CalendarEventAttachedFile_pk");

            entity.ToTable("CalendarEventAttachedFile", tb => tb.HasComment("CalendarEventAttachedFile"));

            entity.HasIndex(e => e.CalendarEventId, "CalendarEventAttachedFile_index_0");

            entity.Property(e => e.Id).HasComment("PK");
            entity.Property(e => e.CalendarEventId).HasComment("Parent CalendarEvent Id");
            entity.Property(e => e.Extension)
                .HasMaxLength(255)
                .HasDefaultValueSql("NULL::character varying")
                .HasComment("Extension");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasComment("Name");
            entity.Property(e => e.Path)
                .HasMaxLength(255)
                .HasComment("Path");
            entity.Property(e => e.Size).HasComment("Size (Byte)");

            entity.HasOne(d => d.CalendarEvent).WithMany(p => p.CalendarEventAttachedFiles)
                .HasForeignKey(d => d.CalendarEventId)
                .HasConstraintName("CalendarEventAttachedFile_fk_0");
        });

        modelBuilder.Entity<CalendarEventReminder>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("CalendarEventReminder_pk");

            entity.ToTable("CalendarEventReminder", tb => tb.HasComment("CalendarEventReminder"));

            entity.HasIndex(e => e.CalendarEventId, "CalendarEventReminder_index_0");

            entity.Property(e => e.Id).HasComment("PK");
            entity.Property(e => e.CalendarEventId).HasComment("Parent Calendar Event Id");
            entity.Property(e => e.DaysBeforeEvent).HasComment("DaysBeforeEvent");
            entity.Property(e => e.HoursBeforeEvent).HasComment("HoursBeforeEvent");
            entity.Property(e => e.Method)
                .HasMaxLength(255)
                .HasComment("Method");
            entity.Property(e => e.MinutesBeforeEvent).HasComment("MinutesBeforeEvent");
            entity.Property(e => e.TimesBeforeEvent).HasComment("TimesBeforeEvent");
            entity.Property(e => e.WeeksBeforeEvent).HasComment("WeeksBeforeEvent");

            entity.HasOne(d => d.CalendarEvent).WithMany(p => p.CalendarEventReminders)
                .HasForeignKey(d => d.CalendarEventId)
                .HasConstraintName("CalendarEventReminder_fk_0");
        });

        modelBuilder.Entity<CalendarRecurrence>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("CalendarRecurrence_pk");

            entity.ToTable("CalendarRecurrence", tb => tb.HasComment("CalendarRecurrence"));

            entity.Property(e => e.Id).HasComment("PK");
            entity.Property(e => e.Count).HasComment("Count");
            entity.Property(e => e.Created)
                .HasComment("Created")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.DayOfMonth).HasComment("DayOfMonth");
            entity.Property(e => e.DayOfWeek)
                .HasMaxLength(255)
                .HasDefaultValueSql("NULL::character varying")
                .HasComment("DayOfWeek");
            entity.Property(e => e.Frequency)
                .HasMaxLength(255)
                .HasComment("Frequency");
            entity.Property(e => e.Interval)
                .HasDefaultValueSql("'1'::bigint")
                .HasComment("Interval");
            entity.Property(e => e.MonthOfYear).HasComment("MonthOfYear");
            entity.Property(e => e.Until)
                .HasComment("Until")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.Updated)
                .HasComment("Updated")
                .HasColumnType("timestamp without time zone");
        });

        modelBuilder.Entity<CalendarShared>(entity =>
        {
            entity.HasKey(e => e.CalendarId).HasName("CalendarShared_pk");

            entity.ToTable("CalendarShared", tb => tb.HasComment("CalendarShared"));

            entity.HasIndex(e => e.CalendarId, "CalendarShared_index_0");

            entity.Property(e => e.CalendarId)
                .ValueGeneratedNever()
                .HasComment("Parent Calendar Id");
            entity.Property(e => e.Anonymous).HasComment("Is anonymous shared");
            entity.Property(e => e.User).HasComment("Is user shared");

            entity.HasOne(d => d.Calendar).WithOne(p => p.CalendarShared)
                .HasForeignKey<CalendarShared>(d => d.CalendarId)
                .HasConstraintName("CalendarShared_fk_0");
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Category_pk");

            entity.ToTable("Category", tb => tb.HasComment("Category"));

            entity.Property(e => e.Id).HasComment("ID");
            entity.Property(e => e.Action)
                .HasMaxLength(255)
                .HasDefaultValueSql("NULL::character varying")
                .HasComment("Action");
            entity.Property(e => e.Controller)
                .HasMaxLength(255)
                .HasComment("Controller");
            entity.Property(e => e.DisplayName)
                .HasMaxLength(255)
                .HasComment("DisplayName");
            entity.Property(e => e.IconPath)
                .HasMaxLength(255)
                .HasComment("IconPath");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasComment("Name");
            entity.Property(e => e.Order).HasComment("Order");
            entity.Property(e => e.Role)
                .HasMaxLength(255)
                .HasDefaultValueSql("'Admin'::character varying")
                .HasComment("Role");
        });

        modelBuilder.Entity<Expenditure>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Expenditure_pk");

            entity.ToTable("Expenditure", tb => tb.HasComment("Expenditure"));

            entity.HasIndex(e => new { e.PaymentMethod, e.AccountEmail }, "Expenditure_index_0");

            entity.HasIndex(e => new { e.AccountEmail, e.Created }, "Expenditure_index_1");

            entity.Property(e => e.Id).HasComment("PK");
            entity.Property(e => e.AccountEmail)
                .HasMaxLength(255)
                .HasComment("Account Email (ID)");
            entity.Property(e => e.Amount)
                .HasPrecision(20, 4)
                .HasComment("Amount");
            entity.Property(e => e.Content)
                .HasMaxLength(255)
                .HasComment("Content");
            entity.Property(e => e.Created)
                .HasDefaultValueSql("now()")
                .HasComment("Created")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.MainClass)
                .HasMaxLength(255)
                .HasComment("MainClass");
            entity.Property(e => e.MyDepositAsset)
                .HasMaxLength(255)
                .HasDefaultValueSql("NULL::character varying")
                .HasComment("MyDepositAsset");
            entity.Property(e => e.Note)
                .HasMaxLength(255)
                .HasDefaultValueSql("''::character varying")
                .HasComment("Note");
            entity.Property(e => e.PaymentMethod)
                .HasMaxLength(255)
                .HasComment("PaymentMethod");
            entity.Property(e => e.SubClass)
                .HasMaxLength(255)
                .HasComment("SubClass");
            entity.Property(e => e.Updated)
                .HasDefaultValueSql("now()")
                .HasComment("Updated")
                .HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.Asset).WithMany(p => p.ExpenditureAssets)
                .HasForeignKey(d => new { d.MyDepositAsset, d.AccountEmail })
                .HasConstraintName("Expenditure_fk_1");

            entity.HasOne(d => d.AssetNavigation).WithMany(p => p.ExpenditureAssetNavigations)
                .HasForeignKey(d => new { d.PaymentMethod, d.AccountEmail })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Expenditure_fk_0");
        });

        modelBuilder.Entity<FixedExpenditure>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("FixedExpenditure_pk");

            entity.ToTable("FixedExpenditure", tb => tb.HasComment("FixedExpenditure"));

            entity.HasIndex(e => new { e.PaymentMethod, e.AccountEmail }, "FixedExpenditure_index_0");

            entity.Property(e => e.Id).HasComment("PK");
            entity.Property(e => e.AccountEmail)
                .HasMaxLength(255)
                .HasComment("Account Email (ID)");
            entity.Property(e => e.Amount)
                .HasPrecision(20, 4)
                .HasComment("Amount");
            entity.Property(e => e.Content)
                .HasMaxLength(255)
                .HasComment("Content");
            entity.Property(e => e.Created)
                .HasDefaultValueSql("now()")
                .HasComment("Created")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.DepositDay).HasComment("DepositDay");
            entity.Property(e => e.DepositMonth).HasComment("DepositMonth");
            entity.Property(e => e.MainClass)
                .HasMaxLength(255)
                .HasComment("MainClass");
            entity.Property(e => e.MaturityDate)
                .HasDefaultValueSql("now()")
                .HasComment("MaturityDate")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.MyDepositAsset)
                .HasMaxLength(255)
                .HasDefaultValueSql("NULL::character varying")
                .HasComment("MyDepositAsset");
            entity.Property(e => e.Note)
                .HasMaxLength(255)
                .HasDefaultValueSql("''::character varying")
                .HasComment("Note");
            entity.Property(e => e.PaymentMethod)
                .HasMaxLength(255)
                .HasComment("PaymentMethod");
            entity.Property(e => e.SubClass)
                .HasMaxLength(255)
                .HasComment("SubClass");
            entity.Property(e => e.Unpunctuality).HasComment("Unpunctuality");
            entity.Property(e => e.Updated)
                .HasDefaultValueSql("now()")
                .HasComment("Updated")
                .HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.Asset).WithMany(p => p.FixedExpenditureAssets)
                .HasForeignKey(d => new { d.MyDepositAsset, d.AccountEmail })
                .HasConstraintName("FixedExpenditure_fk_1");

            entity.HasOne(d => d.AssetNavigation).WithMany(p => p.FixedExpenditureAssetNavigations)
                .HasForeignKey(d => new { d.PaymentMethod, d.AccountEmail })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FixedExpenditure_fk_0");
        });

        modelBuilder.Entity<FixedIncome>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("FixedIncome_pk");

            entity.ToTable("FixedIncome", tb => tb.HasComment("FixedIncome"));

            entity.HasIndex(e => new { e.DepositMyAssetProductName, e.AccountEmail }, "FixedIncome_index_0");

            entity.Property(e => e.Id).HasComment("PK");
            entity.Property(e => e.AccountEmail)
                .HasMaxLength(255)
                .HasComment("Account Email (ID)");
            entity.Property(e => e.Amount)
                .HasPrecision(20, 4)
                .HasComment("Amount");
            entity.Property(e => e.Content)
                .HasMaxLength(255)
                .HasComment("Content");
            entity.Property(e => e.Created)
                .HasDefaultValueSql("now()")
                .HasComment("Created")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.DepositDay).HasComment("DepositDay");
            entity.Property(e => e.DepositMonth).HasComment("DepositMonth");
            entity.Property(e => e.DepositMyAssetProductName)
                .HasMaxLength(255)
                .HasComment("DepositMyAssetProductName");
            entity.Property(e => e.MainClass)
                .HasMaxLength(255)
                .HasComment("MainClass");
            entity.Property(e => e.MaturityDate)
                .HasDefaultValueSql("now()")
                .HasComment("MaturityDate")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.Note)
                .HasMaxLength(255)
                .HasDefaultValueSql("''::character varying")
                .HasComment("Note");
            entity.Property(e => e.SubClass)
                .HasMaxLength(255)
                .HasComment("SubClass");
            entity.Property(e => e.Unpunctuality).HasComment("Unpunctuality");
            entity.Property(e => e.Updated)
                .HasDefaultValueSql("now()")
                .HasComment("Updated")
                .HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.Asset).WithMany(p => p.FixedIncomes)
                .HasForeignKey(d => new { d.DepositMyAssetProductName, d.AccountEmail })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FixedIncome_fk_0");
        });

        modelBuilder.Entity<Income>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Income_pk");

            entity.ToTable("Income", tb => tb.HasComment("Income"));

            entity.HasIndex(e => new { e.DepositMyAssetProductName, e.AccountEmail }, "Income_index_0");

            entity.HasIndex(e => new { e.AccountEmail, e.Created }, "Income_index_1");

            entity.Property(e => e.Id).HasComment("PK");
            entity.Property(e => e.AccountEmail)
                .HasMaxLength(255)
                .HasComment("Account Email (ID)");
            entity.Property(e => e.Amount)
                .HasPrecision(20, 4)
                .HasComment("Amount");
            entity.Property(e => e.Content)
                .HasMaxLength(255)
                .HasComment("Content");
            entity.Property(e => e.Created)
                .HasDefaultValueSql("now()")
                .HasComment("Created")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.DepositMyAssetProductName)
                .HasMaxLength(255)
                .HasComment("DepositMyAssetProductName");
            entity.Property(e => e.MainClass)
                .HasMaxLength(255)
                .HasComment("MainClass");
            entity.Property(e => e.Note)
                .HasMaxLength(255)
                .HasDefaultValueSql("''::character varying")
                .HasComment("Note");
            entity.Property(e => e.SubClass)
                .HasMaxLength(255)
                .HasComment("SubClass");
            entity.Property(e => e.Updated)
                .HasDefaultValueSql("now()")
                .HasComment("Updated")
                .HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.Asset).WithMany(p => p.Incomes)
                .HasForeignKey(d => new { d.DepositMyAssetProductName, d.AccountEmail })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Income_fk_0");
        });

        modelBuilder.Entity<OtherCalendar>(entity =>
        {
            entity.HasKey(e => new { e.AccountEmail, e.CalendarId }).HasName("OtherCalendar_pk");

            entity.ToTable("OtherCalendar", tb => tb.HasComment("OtherCalendar"));

            entity.HasIndex(e => e.AccountEmail, "OtherCalendar_index_0");

            entity.HasIndex(e => e.CalendarId, "OtherCalendar_index_1");

            entity.Property(e => e.AccountEmail)
                .HasMaxLength(255)
                .HasComment("Account Email (ID)");
            entity.Property(e => e.CalendarId).HasComment("Parent Calendar Id");

            entity.HasOne(d => d.Account).WithMany(p => p.OtherCalendars)
                .HasForeignKey(d => d.AccountEmail)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("OtherCalendar_fk_0");

            entity.HasOne(d => d.Calendar).WithMany(p => p.OtherCalendars)
                .HasForeignKey(d => d.CalendarId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("OtherCalendar_fk_1");
        });

        modelBuilder.Entity<SubCategory>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("SubCategory_pk");

            entity.ToTable("SubCategory", tb => tb.HasComment("SubCategory"));

            entity.HasIndex(e => e.CategoryId, "SubCategory_index_0");

            entity.Property(e => e.Id).HasComment("ID");
            entity.Property(e => e.Action)
                .HasMaxLength(255)
                .HasComment("Action");
            entity.Property(e => e.CategoryId).HasComment("Parent Category ID");
            entity.Property(e => e.DisplayName)
                .HasMaxLength(255)
                .HasComment("DisplayName");
            entity.Property(e => e.IconPath)
                .HasMaxLength(255)
                .HasComment("IconPath");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasComment("Name");
            entity.Property(e => e.Order).HasComment("Order");
            entity.Property(e => e.Role)
                .HasMaxLength(255)
                .HasDefaultValueSql("'Admin'::character varying")
                .HasComment("Role");

            entity.HasOne(d => d.Category).WithMany(p => p.SubCategories)
                .HasForeignKey(d => d.CategoryId)
                .HasConstraintName("SubCategory_fk_0");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    /// <summary>Extension point for mapping kept outside the scaffolded file (no implementation at present).</summary>
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
