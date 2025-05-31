using System;
using System.Collections.Generic;
using Maroik.Common.DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace Maroik.Common.DataAccess.Data;

public partial class ApplicationDbContext : DbContext
{
    public ApplicationDbContext()
    {
    }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        ChangeTracker.AutoDetectChangesEnabled = false;
    }

    public virtual DbSet<Account> Accounts { get; set; }

    public virtual DbSet<Asset> Assets { get; set; }

    public virtual DbSet<Board> Boards { get; set; }

    public virtual DbSet<BoardAttachedFile> BoardAttachedFiles { get; set; }

    public virtual DbSet<BoardComment> BoardComments { get; set; }

    public virtual DbSet<Calendar> Calendars { get; set; }

    public virtual DbSet<CalendarEvent> CalendarEvents { get; set; }

    public virtual DbSet<CalendarEventAttachedFile> CalendarEventAttachedFiles { get; set; }

    public virtual DbSet<CalendarEventReminder> CalendarEventReminders { get; set; }

    public virtual DbSet<CalendarRecurrence> CalendarRecurrences { get; set; }

    public virtual DbSet<CalendarShared> CalendarShareds { get; set; }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<Expenditure> Expenditures { get; set; }

    public virtual DbSet<FixedExpenditure> FixedExpenditures { get; set; }

    public virtual DbSet<FixedIncome> FixedIncomes { get; set; }

    public virtual DbSet<Income> Incomes { get; set; }
    public virtual DbSet<OtherCalendar> OtherCalendars { get; set; }

    public virtual DbSet<SubCategory> SubCategories { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasKey(e => e.Email).HasName("Account_pk");

            entity.ToTable("Account", tb => tb.HasComment("Maroik.WebSite 계정"));

            entity.HasIndex(e => e.Nickname, "Account_Nickname_unique").IsUnique();

            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .HasComment("이메일 (ID)");
            entity.Property(e => e.AgreedServiceTerms).HasComment("약관 동의 여부");
            entity.Property(e => e.AvatarImagePath)
                .IsRequired()
                .HasMaxLength(255)
                .HasDefaultValueSql("'/upload/Management/Profile/default-avatar.jpg'::character varying")
                .HasComment("아바타 이미지 경로");
            entity.Property(e => e.Created)
                .HasComment("생성일")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.DefaultMonetaryUnit)
                .HasMaxLength(45)
                .HasDefaultValueSql("NULL::character varying")
                .HasComment("기본 화폐 단위 (KRW, USD, ETC)");
            entity.Property(e => e.Deleted).HasComment("삭제 여부");
            entity.Property(e => e.EmailConfirmed).HasComment("이메일 확인 여부");
            entity.Property(e => e.HashedPassword)
                .IsRequired()
                .HasComment("해시화 된 비밀번호");
            entity.Property(e => e.Locked).HasComment("잠금 여부");
            entity.Property(e => e.LoginAttempt).HasComment("로그인 시도 횟수");
            entity.Property(e => e.Message).HasComment("상태 메시지");
            entity.Property(e => e.Nickname)
                .IsRequired()
                .HasMaxLength(255)
                .HasComment("닉네임");
            entity.Property(e => e.RegistrationToken).HasComment("회원가입 인증 토큰");
            entity.Property(e => e.ResetPasswordToken).HasComment("비밀번호 찾기 인증 토큰");
            entity.Property(e => e.Role)
                .IsRequired()
                .HasMaxLength(255)
                .HasDefaultValueSql("'User'::character varying")
                .HasComment("역할 (Admin 또는 User)");
            entity.Property(e => e.TimeZoneIanaId)
                .IsRequired()
                .HasMaxLength(255)
                .HasDefaultValueSql("'UTC'::character varying")
                .HasComment("IANA TimeZone ID");
            entity.Property(e => e.Updated)
                .HasComment("업데이트일")
                .HasColumnType("timestamp without time zone");
        });

        modelBuilder.Entity<Asset>(entity =>
        {
            entity.HasKey(e => new { e.ProductName, e.AccountEmail }).HasName("Asset_pk");

            entity.ToTable("Asset", tb => tb.HasComment("Maroik.WebSite 자산"));

            entity.HasIndex(e => e.AccountEmail, "Asset_index_0");

            entity.Property(e => e.ProductName)
                .HasMaxLength(255)
                .HasComment("상품명 (은행 계좌명, 증권 계좌명, 현금 등)");
            entity.Property(e => e.AccountEmail)
                .HasMaxLength(255)
                .HasComment("계정 이메일 (ID)");
            entity.Property(e => e.Amount)
                .HasPrecision(18, 2)
                .HasComment("금액");
            entity.Property(e => e.Created)
                .HasComment("생성일")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.Deleted).HasComment("삭제여부");
            entity.Property(e => e.Item)
                .IsRequired()
                .HasMaxLength(255)
                .HasComment("항목 (자유입출금 자산, 신탁 자산, 현금 자산, 저축성 자산, 투자성 자산, 부동산, 동산, 기타 실물 자산, 보험 자산)");
            entity.Property(e => e.MonetaryUnit)
                .IsRequired()
                .HasMaxLength(45)
                .HasComment("화폐 단위 (KRW, USD, ETC)");
            entity.Property(e => e.Note)
                .HasMaxLength(255)
                .HasDefaultValueSql("NULL::character varying")
                .HasComment("비고");
            entity.Property(e => e.Updated)
                .HasComment("업데이트일")
                .HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.AccountEmailNavigation).WithMany(p => p.Assets)
                .HasForeignKey(d => d.AccountEmail)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Asset_fk_0");
        });

        modelBuilder.Entity<Board>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Board_pk");

            entity.ToTable("Board", tb => tb.HasComment("Maroik.WebSite Board"));

            entity.Property(e => e.Id).HasComment("PK");
            entity.Property(e => e.Content)
                .IsRequired()
                .HasComment("내용");
            entity.Property(e => e.Created)
                .HasComment("생성일")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.Deleted).HasComment("삭제 여부");
            entity.Property(e => e.Locked).HasComment("잠금 여부");
            entity.Property(e => e.Noticed).HasComment("공지 여부");
            entity.Property(e => e.Title)
                .IsRequired()
                .HasMaxLength(255)
                .HasComment("제목");
            entity.Property(e => e.Type)
                .IsRequired()
                .HasMaxLength(255)
                .HasComment("형태 (자유)");
            entity.Property(e => e.Updated)
                .HasComment("업데이트일")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.View).HasComment("조회수");
            entity.Property(e => e.Writer)
                .IsRequired()
                .HasMaxLength(255)
                .HasComment("작성자");
        });

        modelBuilder.Entity<BoardAttachedFile>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("BoardAttachedFile_pk");

            entity.ToTable("BoardAttachedFile", tb => tb.HasComment("Maroik.WebSite 게시판 첨부 파일"));

            entity.HasIndex(e => e.BoardId, "BoardAttachedFile_index_0");

            entity.Property(e => e.Id).HasComment("PK");
            entity.Property(e => e.BoardId).HasComment("부모 Board Id");
            entity.Property(e => e.Extension)
                .HasMaxLength(255)
                .HasDefaultValueSql("NULL::character varying")
                .HasComment("확장자");
            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(255)
                .HasComment("이름");
            entity.Property(e => e.Path)
                .IsRequired()
                .HasMaxLength(255)
                .HasComment("경로");
            entity.Property(e => e.Size).HasComment("크기 (Byte)");

            entity.HasOne(d => d.Board).WithMany(p => p.BoardAttachedFiles)
                .HasForeignKey(d => d.BoardId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("BoardAttachedFile_fk_0");
        });

        modelBuilder.Entity<BoardComment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("BoardComment_pk");

            entity.ToTable("BoardComment", tb => tb.HasComment("Maroik.WebSite 게시판 댓글"));

            entity.HasIndex(e => e.BoardId, "BoardComment_index_0");

            entity.Property(e => e.Id).HasComment("PK");
            entity.Property(e => e.AvatarImagePath)
                .IsRequired()
                .HasMaxLength(255)
                .HasComment("계정 아바타 이미지 경로");
            entity.Property(e => e.BoardId).HasComment("게시물 Id");
            entity.Property(e => e.Content)
                .IsRequired()
                .HasComment("내용");
            entity.Property(e => e.Created)
                .HasComment("생성일")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.Deleted).HasComment("삭제 여부");
            entity.Property(e => e.Order).HasComment("순서");
            entity.Property(e => e.Writer)
                .IsRequired()
                .HasMaxLength(255)
                .HasComment("작성자");

            entity.HasOne(d => d.Board).WithMany(p => p.BoardComments)
                .HasForeignKey(d => d.BoardId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("BoardComment_fk_0");
        });

        modelBuilder.Entity<Calendar>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Calendar_pk");

            entity.ToTable("Calendar", tb => tb.HasComment("Maroik.WebSite 달력"));

            entity.HasIndex(e => e.AccountEmail, "Calendar_index_0");

            entity.Property(e => e.Id).HasComment("PK");
            entity.Property(e => e.AccountEmail)
                .IsRequired()
                .HasMaxLength(255)
                .HasComment("계정 이메일 (ID)");
            entity.Property(e => e.Created)
                .HasComment("생성일")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.Description).HasComment("설명");
            entity.Property(e => e.HtmlColorCode)
                .IsRequired()
                .HasMaxLength(10)
                .HasComment("대표색");
            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(255)
                .HasComment("이름");
            entity.Property(e => e.TimeZoneIanaId)
                .IsRequired()
                .HasMaxLength(255)
                .HasComment("IANA TimeZone ID");
            entity.Property(e => e.Updated)
                .HasComment("업데이트일")
                .HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.AccountEmailNavigation).WithMany(p => p.Calendars)
                .HasForeignKey(d => d.AccountEmail)
                .HasConstraintName("Calendar_fk_0");
        });

        modelBuilder.Entity<CalendarEvent>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("CalendarEvent_pk");

            entity.ToTable("CalendarEvent", tb => tb.HasComment("Maroik.WebSite 달력 이벤트"));

            entity.HasIndex(e => e.CalendarId, "CalendarEvent_index_0");

            entity.HasIndex(e => e.RecurrenceId, "CalendarEvent_index_1");

            entity.Property(e => e.Id).HasComment("PK");
            entity.Property(e => e.AllDay).HasComment("종일 이벤트 여부");
            entity.Property(e => e.CalendarId).HasComment("부모 Calendar Id");
            entity.Property(e => e.Created)
                .HasComment("생성일")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.Description).HasComment("설명");
            entity.Property(e => e.EndDate)
                .HasComment("종료 날짜 및 시간")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.EndDateTimeZoneIanaId)
                .HasMaxLength(255)
                .HasDefaultValueSql("NULL::character varying")
                .HasComment("종료 날짜 및 시간 (IANA TimeZone ID)");
            entity.Property(e => e.Location)
                .HasMaxLength(255)
                .HasDefaultValueSql("NULL::character varying")
                .HasComment("위치");
            entity.Property(e => e.RecurrenceId).HasComment("반복 규칙 ID (옵션)");
            entity.Property(e => e.StartDate)
                .HasComment("시작 날짜 및 시간")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.StartDateTimeZoneIanaId)
                .HasMaxLength(255)
                .HasDefaultValueSql("NULL::character varying")
                .HasComment("시작 날짜 및 시간 (IANA TimeZone ID)");
            entity.Property(e => e.Status)
                .IsRequired()
                .HasMaxLength(255)
                .HasDefaultValueSql("'Busy'::character varying")
                .HasComment("이벤트 상태");
            entity.Property(e => e.Title)
                .IsRequired()
                .HasMaxLength(255)
                .HasComment("제목");
            entity.Property(e => e.Updated)
                .HasComment("업데이트일")
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

            entity.ToTable("CalendarEventAttachedFile", tb => tb.HasComment("Maroik.WebSite 달력 이벤트 첨부파일"));

            entity.HasIndex(e => e.CalendarEventId, "CalendarEventAttachedFile_index_0");

            entity.Property(e => e.Id).HasComment("PK");
            entity.Property(e => e.CalendarEventId).HasComment("부모 CalendarEvent Id");
            entity.Property(e => e.Extension)
                .HasMaxLength(255)
                .HasDefaultValueSql("NULL::character varying")
                .HasComment("확장자");
            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(255)
                .HasComment("이름");
            entity.Property(e => e.Path)
                .IsRequired()
                .HasMaxLength(255)
                .HasComment("경로");
            entity.Property(e => e.Size).HasComment("크기 (Byte)");

            entity.HasOne(d => d.CalendarEvent).WithMany(p => p.CalendarEventAttachedFiles)
                .HasForeignKey(d => d.CalendarEventId)
                .HasConstraintName("CalendarEventAttachedFile_fk_0");
        });

        modelBuilder.Entity<CalendarEventReminder>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("CalendarEventReminder_pk");

            entity.ToTable("CalendarEventReminder", tb => tb.HasComment("Maroik.WebSite 달력 이벤트 알림"));

            entity.HasIndex(e => e.CalendarEventId, "CalendarEventReminder_index_0");

            entity.Property(e => e.Id).HasComment("PK");
            entity.Property(e => e.CalendarEventId).HasComment("부모 Calendar Event Id");
            entity.Property(e => e.DaysBeforeEvent).HasComment("이벤트 발생 전 알림 시간 (일 단위)");
            entity.Property(e => e.HoursBeforeEvent).HasComment("이벤트 발생 전 알림 시간 (시간 단위)");
            entity.Property(e => e.Method)
                .IsRequired()
                .HasMaxLength(255)
                .HasComment("알림 방법");
            entity.Property(e => e.MinutesBeforeEvent).HasComment("이벤트 발생 전 알림 시간 (분 단위)");
            entity.Property(e => e.TimesBeforeEvent).HasComment("이벤트 발생 전 알림 시간 (시간분 단위)");
            entity.Property(e => e.WeeksBeforeEvent).HasComment("이벤트 발생 전 알림 시간 (주 단위)");

            entity.HasOne(d => d.CalendarEvent).WithMany(p => p.CalendarEventReminders)
                .HasForeignKey(d => d.CalendarEventId)
                .HasConstraintName("CalendarEventReminder_fk_0");
        });

        modelBuilder.Entity<CalendarRecurrence>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("CalendarRecurrence_pk");

            entity.ToTable("CalendarRecurrence", tb => tb.HasComment("Maroik.WebSite 달력 반복"));

            entity.Property(e => e.Id).HasComment("PK");
            entity.Property(e => e.Count).HasComment("반복 횟수 (옵션)");
            entity.Property(e => e.Created)
                .HasComment("생성일")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.DayOfMonth).HasComment("매월 반복일 (1~31, 옵션)");
            entity.Property(e => e.DayOfWeek)
                .HasMaxLength(255)
                .HasDefaultValueSql("NULL::character varying")
                .HasComment("반복 요일");
            entity.Property(e => e.Frequency)
                .IsRequired()
                .HasMaxLength(255)
                .HasComment("반복 주기");
            entity.Property(e => e.Interval)
                .HasDefaultValueSql("'1'::bigint")
                .HasComment("반복 간격");
            entity.Property(e => e.MonthOfYear).HasComment("매년 반복월 (1~12, 옵션)");
            entity.Property(e => e.Until)
                .HasComment("반복 종료 날짜 (옵션)")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.Updated)
                .HasComment("업데이트일")
                .HasColumnType("timestamp without time zone");
        });

        modelBuilder.Entity<CalendarShared>(entity =>
        {
            entity.HasKey(e => e.CalendarId).HasName("CalendarShared_pk");

            entity.ToTable("CalendarShared", tb => tb.HasComment("Maroik.WebSite 달력 공유"));

            entity.HasIndex(e => e.CalendarId, "CalendarShared_index_0");

            entity.Property(e => e.CalendarId)
                .ValueGeneratedNever()
                .HasComment("부모 Calendar Id");
            entity.Property(e => e.Anonymous).HasComment("Anonymous 공유 여부");
            entity.Property(e => e.User).HasComment("User 공유 여부");

            entity.HasOne(d => d.Calendar).WithOne(p => p.CalendarShared)
                .HasForeignKey<CalendarShared>(d => d.CalendarId)
                .HasConstraintName("CalendarShared_fk_0");
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Category_pk");

            entity.ToTable("Category", tb => tb.HasComment("카테고리 /*Maroik.WebSite의 로그인 후 접근 가능한 좌측 SideBar 설정 시 사용*/"));

            entity.Property(e => e.Id).HasComment("ID");
            entity.Property(e => e.Action)
                .HasMaxLength(255)
                .HasDefaultValueSql("NULL::character varying")
                .HasComment("접근 MVC Action 명 /*이 값이 없으면 하위 카테고리 존재*/");
            entity.Property(e => e.Controller)
                .IsRequired()
                .HasMaxLength(255)
                .HasComment("접근 MVC Controller 명");
            entity.Property(e => e.DisplayName)
                .IsRequired()
                .HasMaxLength(255)
                .HasComment("표시이름");
            entity.Property(e => e.IconPath)
                .IsRequired()
                .HasMaxLength(255)
                .HasComment("표시 아이콘 경로 /*FontAwesome 사용*/");
            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(255)
                .HasComment("이름");
            entity.Property(e => e.Order).HasComment("출력 순서");
            entity.Property(e => e.Role)
                .IsRequired()
                .HasMaxLength(255)
                .HasDefaultValueSql("'Admin'::character varying")
                .HasComment("접근 권한 설정");
        });

        modelBuilder.Entity<Expenditure>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Expenditure_pk");

            entity.ToTable("Expenditure", tb => tb.HasComment("Maroik.WebSite 지출"));

            entity.HasIndex(e => new { e.PaymentMethod, e.AccountEmail }, "Expenditure_index_0");

            entity.Property(e => e.Id).HasComment("PK");
            entity.Property(e => e.AccountEmail)
                .IsRequired()
                .HasMaxLength(255)
                .HasComment("계정 이메일 (ID)");
            entity.Property(e => e.Amount)
                .HasPrecision(18, 2)
                .HasComment("금액");
            entity.Property(e => e.Content)
                .IsRequired()
                .HasMaxLength(255)
                .HasComment("내용 (A마트/B카드/C음식점/D도서관)");
            entity.Property(e => e.Created).HasComment("생성일");
            entity.Property(e => e.MainClass)
                .IsRequired()
                .HasMaxLength(255)
                .HasComment("대분류 (정기저축/비소비지출/소비지출)");
            entity.Property(e => e.MyDepositAsset)
                .HasMaxLength(255)
                .HasDefaultValueSql("NULL::character varying")
                .HasComment("내 입금 자산 (자산 상품명/현금) (지출 중 [예적금, 내자산이체, 투자, 공적연금, 부채상환]일 때 사용)");
            entity.Property(e => e.Note)
                .HasMaxLength(255)
                .HasDefaultValueSql("NULL::character varying")
                .HasComment("비고");
            entity.Property(e => e.PaymentMethod)
                .IsRequired()
                .HasMaxLength(255)
                .HasComment("결제 수단 (자산 상품명/현금)");
            entity.Property(e => e.SubClass)
                .IsRequired()
                .HasMaxLength(255)
                .HasComment("소분류 (예적금/내자산이체/투자 | 공적연금/부채상환/세금/사회보험/가구간 이전지출/비영리단체 이전 | (식비/외식비)/(주거/용품비)/교육비/의료비/교통비/통신비/(여가/문화)/(의류/신발)/용돈/보장성보험/기타지출/미파악지출)");
            entity.Property(e => e.Updated).HasComment("업데이트일");

            entity.HasOne(d => d.Asset).WithMany(p => p.Expenditures)
                .HasForeignKey(d => new { d.PaymentMethod, d.AccountEmail })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Expenditure_fk_0");
        });

        modelBuilder.Entity<FixedExpenditure>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("FixedExpenditure_pk");

            entity.ToTable("FixedExpenditure", tb => tb.HasComment("Maroik.WebSite 고정지출"));

            entity.HasIndex(e => new { e.PaymentMethod, e.AccountEmail }, "FixedExpenditure_index_0");

            entity.Property(e => e.Id).HasComment("PK");
            entity.Property(e => e.AccountEmail)
                .IsRequired()
                .HasMaxLength(255)
                .HasComment("계정 이메일 (ID)");
            entity.Property(e => e.Amount)
                .HasPrecision(18, 2)
                .HasComment("금액");
            entity.Property(e => e.Content)
                .IsRequired()
                .HasMaxLength(255)
                .HasComment("내용 (A마트/B카드/C음식점/D도서관)");
            entity.Property(e => e.Created).HasComment("생성일");
            entity.Property(e => e.DepositDay).HasComment("입금일");
            entity.Property(e => e.DepositMonth).HasComment("입금월");
            entity.Property(e => e.MainClass)
                .IsRequired()
                .HasMaxLength(255)
                .HasComment("대분류 (정기저축/비소비지출/소비지출)");
            entity.Property(e => e.MaturityDate).HasComment("만기일");
            entity.Property(e => e.MyDepositAsset)
                .HasMaxLength(255)
                .HasDefaultValueSql("NULL::character varying")
                .HasComment("내 입금 자산 (자산 상품명/현금) (지출 중 [예적금, 내자산이체, 투자, 공적연금, 부채상환]일 때 사용)");
            entity.Property(e => e.Note)
                .HasMaxLength(255)
                .HasDefaultValueSql("NULL::character varying")
                .HasComment("비고");
            entity.Property(e => e.PaymentMethod)
                .IsRequired()
                .HasMaxLength(255)
                .HasComment("결제 수단 (자산 상품명/현금)");
            entity.Property(e => e.SubClass)
                .IsRequired()
                .HasMaxLength(255)
                .HasComment("소분류 (예적금/내자산이체/투자 | 공적연금/부채상환/세금/사회보험/가구간 이전지출/비영리단체 이전 | (식비/외식비)/(주거/용품비)/교육비/의료비/교통비/통신비/(여가/문화)/(의류/신발)/용돈/보장성보험/기타지출/미파악지출)");
            entity.Property(e => e.Unpunctuality).HasComment("시간 미엄수 (고정 지출 시간 약속을 지키지 않았을 때 계속 알림에 표시하는 용도)");
            entity.Property(e => e.Updated).HasComment("업데이트일");

            entity.HasOne(d => d.Asset).WithMany(p => p.FixedExpenditures)
                .HasForeignKey(d => new { d.PaymentMethod, d.AccountEmail })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FixedExpenditure_fk_0");
        });

        modelBuilder.Entity<FixedIncome>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("FixedIncome_pk");

            entity.ToTable("FixedIncome", tb => tb.HasComment("Maroik.WebSite 고정수입"));

            entity.HasIndex(e => new { e.DepositMyAssetProductName, e.AccountEmail }, "FixedIncome_index_0");

            entity.Property(e => e.Id).HasComment("PK");
            entity.Property(e => e.AccountEmail)
                .IsRequired()
                .HasMaxLength(255)
                .HasComment("계정 이메일 (ID)");
            entity.Property(e => e.Amount)
                .HasPrecision(18, 2)
                .HasComment("금액");
            entity.Property(e => e.Content)
                .IsRequired()
                .HasMaxLength(255)
                .HasComment("내용 (회사명/사업명)");
            entity.Property(e => e.Created).HasComment("생성일");
            entity.Property(e => e.DepositDay).HasComment("입금일");
            entity.Property(e => e.DepositMonth).HasComment("입금월");
            entity.Property(e => e.DepositMyAssetProductName)
                .IsRequired()
                .HasMaxLength(255)
                .HasComment("입금 자산 (자산 상품명/현금)");
            entity.Property(e => e.MainClass)
                .IsRequired()
                .HasMaxLength(255)
                .HasComment("대분류 (정기수입/비정기수입)");
            entity.Property(e => e.MaturityDate).HasComment("만기일");
            entity.Property(e => e.Note)
                .HasMaxLength(255)
                .HasDefaultValueSql("NULL::character varying")
                .HasComment("비고");
            entity.Property(e => e.SubClass)
                .IsRequired()
                .HasMaxLength(255)
                .HasComment("소분류 (근로수입/사업수입/연금수입/금융소득/임대수입/기타수입)");
            entity.Property(e => e.Unpunctuality).HasComment("시간 미엄수 (고정 수입 시간 약속을 지키지 않았을 때 계속 알림에 표시하는 용도)");
            entity.Property(e => e.Updated).HasComment("업데이트일");

            entity.HasOne(d => d.Asset).WithMany(p => p.FixedIncomes)
                .HasForeignKey(d => new { d.DepositMyAssetProductName, d.AccountEmail })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FixedIncome_fk_0");
        });

        modelBuilder.Entity<Income>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Income_pk");

            entity.ToTable("Income", tb => tb.HasComment("Maroik.WebSite 수입"));

            entity.HasIndex(e => new { e.DepositMyAssetProductName, e.AccountEmail }, "Income_index_0");

            entity.Property(e => e.Id).HasComment("PK");
            entity.Property(e => e.AccountEmail)
                .IsRequired()
                .HasMaxLength(255)
                .HasComment("계정 이메일 (ID)");
            entity.Property(e => e.Amount)
                .HasPrecision(18, 2)
                .HasComment("금액");
            entity.Property(e => e.Content)
                .IsRequired()
                .HasMaxLength(255)
                .HasComment("내용 (회사명/사업명)");
            entity.Property(e => e.Created).HasComment("생성일");
            entity.Property(e => e.DepositMyAssetProductName)
                .IsRequired()
                .HasMaxLength(255)
                .HasComment("입금 자산 (자산 상품명/현금)");
            entity.Property(e => e.MainClass)
                .IsRequired()
                .HasMaxLength(255)
                .HasComment("대분류 (정기수입/비정기수입)");
            entity.Property(e => e.Note)
                .HasMaxLength(255)
                .HasDefaultValueSql("NULL::character varying")
                .HasComment("비고");
            entity.Property(e => e.SubClass)
                .IsRequired()
                .HasMaxLength(255)
                .HasComment("소분류 (근로수입/사업수입/연금수입/금융소득/임대수입/기타수입)");
            entity.Property(e => e.Updated).HasComment("업데이트일");

            entity.HasOne(d => d.Asset).WithMany(p => p.Incomes)
                .HasForeignKey(d => new { d.DepositMyAssetProductName, d.AccountEmail })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Income_fk_0");
        });
        
        _ = modelBuilder.Entity<OtherCalendar>(entity =>
        {
            _ = entity.HasKey(e => new { e.AccountEmail, e.CalendarId })
                .HasName("OtherCalendar_pk");

            _ = entity.ToTable("OtherCalendar", tb => 
                tb.HasComment("Maroik.WebSite 다른 달력"));

            _ = entity.HasIndex(e => e.AccountEmail)
                .HasDatabaseName("OtherCalendar_index_0");

            _ = entity.HasIndex(e => e.CalendarId)
                .HasDatabaseName("OtherCalendar_index_1");

            _ = entity.Property(e => e.AccountEmail)
                .HasComment("계정 이메일 (ID)");

            _ = entity.Property(e => e.CalendarId)
                .HasComment("부모 Calendar Id");

            _ = entity.HasOne(d => d.Account)
                .WithMany(p => p.OtherCalendars)
                .HasForeignKey(d => d.AccountEmail)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("OtherCalendar_fk_0");

            _ = entity.HasOne(d => d.Calendar)
                .WithMany(p => p.OtherCalendars)
                .HasForeignKey(d => d.CalendarId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("OtherCalendar_fk_1");
        });
        
        modelBuilder.Entity<SubCategory>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("SubCategory_pk");

            entity.ToTable("SubCategory", tb => tb.HasComment("서브 카테고리 /*Maroik.WebSite의 로그인 후 접근 가능한 좌측 SideBar 설정 시 사용*/"));

            entity.HasIndex(e => e.CategoryId, "SubCategory_index_0");

            entity.Property(e => e.Id).HasComment("ID");
            entity.Property(e => e.Action)
                .IsRequired()
                .HasMaxLength(255)
                .HasComment("접근 MVC Action 명 /*이 값이 없으면 하위 카테고리 존재*/");
            entity.Property(e => e.CategoryId).HasComment("부모 카테고리 ID");
            entity.Property(e => e.DisplayName)
                .IsRequired()
                .HasMaxLength(255)
                .HasComment("표시이름");
            entity.Property(e => e.IconPath)
                .IsRequired()
                .HasMaxLength(255)
                .HasComment("표시 아이콘 경로 /*FontAwesome 사용*/");
            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(255)
                .HasComment("이름");
            entity.Property(e => e.Order).HasComment("출력 순서");
            entity.Property(e => e.Role)
                .IsRequired()
                .HasMaxLength(255)
                .HasDefaultValueSql("'Admin'::character varying")
                .HasComment("접근 권한 설정");

            entity.HasOne(d => d.Category).WithMany(p => p.SubCategories)
                .HasForeignKey(d => d.CategoryId)
                .HasConstraintName("SubCategory_fk_0");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
