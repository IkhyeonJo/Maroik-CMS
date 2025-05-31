--
-- PostgreSQL database dump
--

-- Dumped from database version 17.5 (Debian 17.5-1.pgdg120+1)
-- Dumped by pg_dump version 17.5 (Debian 17.5-1.pgdg120+1)

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET transaction_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

--
-- Name: public; Type: SCHEMA; Schema: -; Owner: postgres
--

-- *not* creating schema, since initdb creates it


ALTER SCHEMA public OWNER TO postgres;

--
-- Name: SCHEMA public; Type: COMMENT; Schema: -; Owner: postgres
--

COMMENT ON SCHEMA public IS '';


SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: Account; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."Account" (
    "Email" character varying(255) NOT NULL,
    "HashedPassword" text NOT NULL,
    "Nickname" character varying(255) NOT NULL,
    "AvatarImagePath" character varying(255) DEFAULT '/upload/Management/Profile/default-avatar.jpg'::character varying NOT NULL,
    "Role" character varying(255) DEFAULT 'User'::character varying NOT NULL,
    "TimeZoneIanaId" character varying(255) DEFAULT 'UTC'::character varying NOT NULL,
    "DefaultMonetaryUnit" character varying(45) DEFAULT NULL::character varying,
    "Locked" boolean NOT NULL,
    "LoginAttempt" bigint NOT NULL,
    "EmailConfirmed" boolean NOT NULL,
    "AgreedServiceTerms" boolean NOT NULL,
    "RegistrationToken" text,
    "ResetPasswordToken" text,
    "Created" timestamp without time zone DEFAULT '1900-01-01 00:00:00'::timestamp without time zone NOT NULL,
    "Updated" timestamp without time zone DEFAULT '1900-01-01 00:00:00'::timestamp without time zone NOT NULL,
    "Message" text,
    "Deleted" boolean NOT NULL,
    CONSTRAINT "Account_Role_check" CHECK (((("Role")::text = 'Admin'::text) OR (("Role")::text = 'User'::text)))
);


ALTER TABLE public."Account" OWNER TO postgres;

--
-- Name: TABLE "Account"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON TABLE public."Account" IS 'Maroik.WebSite 계정';


--
-- Name: COLUMN "Account"."Email"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Account"."Email" IS '이메일 (ID)';


--
-- Name: COLUMN "Account"."HashedPassword"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Account"."HashedPassword" IS '해시화 된 비밀번호';


--
-- Name: COLUMN "Account"."Nickname"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Account"."Nickname" IS '닉네임';


--
-- Name: COLUMN "Account"."AvatarImagePath"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Account"."AvatarImagePath" IS '아바타 이미지 경로';


--
-- Name: COLUMN "Account"."Role"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Account"."Role" IS '역할 (Admin 또는 User)';


--
-- Name: COLUMN "Account"."TimeZoneIanaId"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Account"."TimeZoneIanaId" IS 'IANA TimeZone ID';


--
-- Name: COLUMN "Account"."DefaultMonetaryUnit"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Account"."DefaultMonetaryUnit" IS '기본 화폐 단위 (KRW, USD, ETC)';


--
-- Name: COLUMN "Account"."Locked"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Account"."Locked" IS '잠금 여부';


--
-- Name: COLUMN "Account"."LoginAttempt"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Account"."LoginAttempt" IS '로그인 시도 횟수';


--
-- Name: COLUMN "Account"."EmailConfirmed"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Account"."EmailConfirmed" IS '이메일 확인 여부';


--
-- Name: COLUMN "Account"."AgreedServiceTerms"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Account"."AgreedServiceTerms" IS '약관 동의 여부';


--
-- Name: COLUMN "Account"."RegistrationToken"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Account"."RegistrationToken" IS '회원가입 인증 토큰';


--
-- Name: COLUMN "Account"."ResetPasswordToken"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Account"."ResetPasswordToken" IS '비밀번호 찾기 인증 토큰';


--
-- Name: COLUMN "Account"."Created"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Account"."Created" IS '생성일';


--
-- Name: COLUMN "Account"."Updated"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Account"."Updated" IS '업데이트일';


--
-- Name: COLUMN "Account"."Message"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Account"."Message" IS '상태 메시지';


--
-- Name: COLUMN "Account"."Deleted"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Account"."Deleted" IS '삭제 여부';


--
-- Name: Asset; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."Asset" (
    "ProductName" character varying(255) NOT NULL,
    "AccountEmail" character varying(255) NOT NULL,
    "Item" character varying(255) NOT NULL,
    "Amount" numeric(18,2) NOT NULL,
    "MonetaryUnit" character varying(45) NOT NULL,
    "Created" timestamp without time zone NOT NULL,
    "Updated" timestamp without time zone NOT NULL,
    "Note" character varying(255) DEFAULT NULL::character varying,
    "Deleted" boolean NOT NULL,
    CONSTRAINT "Asset_Item_check" CHECK ((("Item")::text = ANY (ARRAY[('FreeDepositAndWithdrawal'::character varying)::text, ('TrustAsset'::character varying)::text, ('CashAsset'::character varying)::text, ('SavingsAsset'::character varying)::text, ('InvestmentAsset'::character varying)::text, ('RealEstate'::character varying)::text, ('Movables'::character varying)::text, ('OtherPhysicalAsset'::character varying)::text, ('InsuranceAsset'::character varying)::text])))
);


ALTER TABLE public."Asset" OWNER TO postgres;

--
-- Name: TABLE "Asset"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON TABLE public."Asset" IS 'Maroik.WebSite 자산';


--
-- Name: COLUMN "Asset"."ProductName"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Asset"."ProductName" IS '상품명 (은행 계좌명, 증권 계좌명, 현금 등)';


--
-- Name: COLUMN "Asset"."AccountEmail"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Asset"."AccountEmail" IS '계정 이메일 (ID)';


--
-- Name: COLUMN "Asset"."Item"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Asset"."Item" IS '항목 (자유입출금 자산, 신탁 자산, 현금 자산, 저축성 자산, 투자성 자산, 부동산, 동산, 기타 실물 자산, 보험 자산)';


--
-- Name: COLUMN "Asset"."Amount"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Asset"."Amount" IS '금액';


--
-- Name: COLUMN "Asset"."MonetaryUnit"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Asset"."MonetaryUnit" IS '화폐 단위 (KRW, USD, ETC)';


--
-- Name: COLUMN "Asset"."Created"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Asset"."Created" IS '생성일';


--
-- Name: COLUMN "Asset"."Updated"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Asset"."Updated" IS '업데이트일';


--
-- Name: COLUMN "Asset"."Note"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Asset"."Note" IS '비고';


--
-- Name: COLUMN "Asset"."Deleted"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Asset"."Deleted" IS '삭제여부';


--
-- Name: Board; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."Board" (
    "Id" bigint NOT NULL,
    "Type" character varying(255) NOT NULL,
    "Title" character varying(255) NOT NULL,
    "Content" text NOT NULL,
    "Writer" character varying(255) NOT NULL,
    "Created" timestamp without time zone NOT NULL,
    "Updated" timestamp without time zone NOT NULL,
    "View" bigint NOT NULL,
    "Deleted" boolean NOT NULL,
    "Locked" boolean NOT NULL,
    "Noticed" boolean NOT NULL,
    CONSTRAINT "Board_Type_check" CHECK ((("Type")::text = ANY (ARRAY[('FreeForum'::character varying)::text, ('PrivateNote'::character varying)::text])))
);


ALTER TABLE public."Board" OWNER TO postgres;

--
-- Name: TABLE "Board"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON TABLE public."Board" IS 'Maroik.WebSite Board';


--
-- Name: COLUMN "Board"."Id"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Board"."Id" IS 'PK';


--
-- Name: COLUMN "Board"."Type"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Board"."Type" IS '형태 (자유)';


--
-- Name: COLUMN "Board"."Title"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Board"."Title" IS '제목';


--
-- Name: COLUMN "Board"."Content"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Board"."Content" IS '내용';


--
-- Name: COLUMN "Board"."Writer"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Board"."Writer" IS '작성자';


--
-- Name: COLUMN "Board"."Created"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Board"."Created" IS '생성일';


--
-- Name: COLUMN "Board"."Updated"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Board"."Updated" IS '업데이트일';


--
-- Name: COLUMN "Board"."View"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Board"."View" IS '조회수';


--
-- Name: COLUMN "Board"."Deleted"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Board"."Deleted" IS '삭제 여부';


--
-- Name: COLUMN "Board"."Locked"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Board"."Locked" IS '잠금 여부';


--
-- Name: COLUMN "Board"."Noticed"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Board"."Noticed" IS '공지 여부';


--
-- Name: BoardAttachedFile; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."BoardAttachedFile" (
    "Id" bigint NOT NULL,
    "BoardId" bigint NOT NULL,
    "Size" bigint NOT NULL,
    "Name" character varying(255) NOT NULL,
    "Extension" character varying(255) DEFAULT NULL::character varying,
    "Path" character varying(255) NOT NULL
);


ALTER TABLE public."BoardAttachedFile" OWNER TO postgres;

--
-- Name: TABLE "BoardAttachedFile"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON TABLE public."BoardAttachedFile" IS 'Maroik.WebSite 게시판 첨부 파일';


--
-- Name: COLUMN "BoardAttachedFile"."Id"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."BoardAttachedFile"."Id" IS 'PK';


--
-- Name: COLUMN "BoardAttachedFile"."BoardId"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."BoardAttachedFile"."BoardId" IS '부모 Board Id';


--
-- Name: COLUMN "BoardAttachedFile"."Size"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."BoardAttachedFile"."Size" IS '크기 (Byte)';


--
-- Name: COLUMN "BoardAttachedFile"."Name"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."BoardAttachedFile"."Name" IS '이름';


--
-- Name: COLUMN "BoardAttachedFile"."Extension"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."BoardAttachedFile"."Extension" IS '확장자';


--
-- Name: COLUMN "BoardAttachedFile"."Path"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."BoardAttachedFile"."Path" IS '경로';


--
-- Name: BoardAttachedFile_Id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public."BoardAttachedFile_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public."BoardAttachedFile_Id_seq" OWNER TO postgres;

--
-- Name: BoardAttachedFile_Id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public."BoardAttachedFile_Id_seq" OWNED BY public."BoardAttachedFile"."Id";


--
-- Name: BoardComment; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."BoardComment" (
    "Id" bigint NOT NULL,
    "BoardId" bigint NOT NULL,
    "Order" bigint NOT NULL,
    "AvatarImagePath" character varying(255) NOT NULL,
    "Writer" character varying(255) NOT NULL,
    "Content" text NOT NULL,
    "Created" timestamp without time zone NOT NULL,
    "Deleted" boolean NOT NULL
);


ALTER TABLE public."BoardComment" OWNER TO postgres;

--
-- Name: TABLE "BoardComment"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON TABLE public."BoardComment" IS 'Maroik.WebSite 게시판 댓글';


--
-- Name: COLUMN "BoardComment"."Id"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."BoardComment"."Id" IS 'PK';


--
-- Name: COLUMN "BoardComment"."BoardId"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."BoardComment"."BoardId" IS '게시물 Id';


--
-- Name: COLUMN "BoardComment"."Order"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."BoardComment"."Order" IS '순서';


--
-- Name: COLUMN "BoardComment"."AvatarImagePath"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."BoardComment"."AvatarImagePath" IS '계정 아바타 이미지 경로';


--
-- Name: COLUMN "BoardComment"."Writer"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."BoardComment"."Writer" IS '작성자';


--
-- Name: COLUMN "BoardComment"."Content"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."BoardComment"."Content" IS '내용';


--
-- Name: COLUMN "BoardComment"."Created"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."BoardComment"."Created" IS '생성일';


--
-- Name: COLUMN "BoardComment"."Deleted"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."BoardComment"."Deleted" IS '삭제 여부';


--
-- Name: BoardComment_Id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public."BoardComment_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public."BoardComment_Id_seq" OWNER TO postgres;

--
-- Name: BoardComment_Id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public."BoardComment_Id_seq" OWNED BY public."BoardComment"."Id";


--
-- Name: Board_Id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public."Board_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public."Board_Id_seq" OWNER TO postgres;

--
-- Name: Board_Id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public."Board_Id_seq" OWNED BY public."Board"."Id";


--
-- Name: Calendar; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."Calendar" (
    "Id" bigint NOT NULL,
    "AccountEmail" character varying(255) NOT NULL,
    "Name" character varying(255) NOT NULL,
    "Description" text,
    "TimeZoneIanaId" character varying(255) NOT NULL,
    "HtmlColorCode" character varying(10) NOT NULL,
    "Created" timestamp without time zone NOT NULL,
    "Updated" timestamp without time zone NOT NULL
);


ALTER TABLE public."Calendar" OWNER TO postgres;

--
-- Name: TABLE "Calendar"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON TABLE public."Calendar" IS 'Maroik.WebSite 달력';


--
-- Name: COLUMN "Calendar"."Id"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Calendar"."Id" IS 'PK';


--
-- Name: COLUMN "Calendar"."AccountEmail"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Calendar"."AccountEmail" IS '계정 이메일 (ID)';


--
-- Name: COLUMN "Calendar"."Name"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Calendar"."Name" IS '이름';


--
-- Name: COLUMN "Calendar"."Description"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Calendar"."Description" IS '설명';


--
-- Name: COLUMN "Calendar"."TimeZoneIanaId"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Calendar"."TimeZoneIanaId" IS 'IANA TimeZone ID';


--
-- Name: COLUMN "Calendar"."HtmlColorCode"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Calendar"."HtmlColorCode" IS '대표색';


--
-- Name: COLUMN "Calendar"."Created"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Calendar"."Created" IS '생성일';


--
-- Name: COLUMN "Calendar"."Updated"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Calendar"."Updated" IS '업데이트일';


--
-- Name: CalendarEvent; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."CalendarEvent" (
    "Id" bigint NOT NULL,
    "CalendarId" bigint NOT NULL,
    "Title" character varying(255) NOT NULL,
    "Description" text,
    "AllDay" boolean NOT NULL,
    "StartDate" timestamp without time zone NOT NULL,
    "EndDate" timestamp without time zone NOT NULL,
    "StartDateTimeZoneIanaId" character varying(255) DEFAULT NULL::character varying,
    "EndDateTimeZoneIanaId" character varying(255) DEFAULT NULL::character varying,
    "Location" character varying(255) DEFAULT NULL::character varying,
    "Status" character varying(255) DEFAULT 'Busy'::character varying NOT NULL,
    "RecurrenceId" bigint,
    "Created" timestamp without time zone NOT NULL,
    "Updated" timestamp without time zone NOT NULL,
    CONSTRAINT "CalendarEvent_Status_check" CHECK ((("Status")::text = ANY (ARRAY[('Busy'::character varying)::text, ('Free'::character varying)::text])))
);


ALTER TABLE public."CalendarEvent" OWNER TO postgres;

--
-- Name: TABLE "CalendarEvent"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON TABLE public."CalendarEvent" IS 'Maroik.WebSite 달력 이벤트';


--
-- Name: COLUMN "CalendarEvent"."Id"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarEvent"."Id" IS 'PK';


--
-- Name: COLUMN "CalendarEvent"."CalendarId"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarEvent"."CalendarId" IS '부모 Calendar Id';


--
-- Name: COLUMN "CalendarEvent"."Title"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarEvent"."Title" IS '제목';


--
-- Name: COLUMN "CalendarEvent"."Description"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarEvent"."Description" IS '설명';


--
-- Name: COLUMN "CalendarEvent"."AllDay"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarEvent"."AllDay" IS '종일 이벤트 여부';


--
-- Name: COLUMN "CalendarEvent"."StartDate"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarEvent"."StartDate" IS '시작 날짜 및 시간';


--
-- Name: COLUMN "CalendarEvent"."EndDate"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarEvent"."EndDate" IS '종료 날짜 및 시간';


--
-- Name: COLUMN "CalendarEvent"."StartDateTimeZoneIanaId"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarEvent"."StartDateTimeZoneIanaId" IS '시작 날짜 및 시간 (IANA TimeZone ID)';


--
-- Name: COLUMN "CalendarEvent"."EndDateTimeZoneIanaId"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarEvent"."EndDateTimeZoneIanaId" IS '종료 날짜 및 시간 (IANA TimeZone ID)';


--
-- Name: COLUMN "CalendarEvent"."Location"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarEvent"."Location" IS '위치';


--
-- Name: COLUMN "CalendarEvent"."Status"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarEvent"."Status" IS '이벤트 상태';


--
-- Name: COLUMN "CalendarEvent"."RecurrenceId"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarEvent"."RecurrenceId" IS '반복 규칙 ID (옵션)';


--
-- Name: COLUMN "CalendarEvent"."Created"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarEvent"."Created" IS '생성일';


--
-- Name: COLUMN "CalendarEvent"."Updated"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarEvent"."Updated" IS '업데이트일';


--
-- Name: CalendarEventAttachedFile; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."CalendarEventAttachedFile" (
    "Id" bigint NOT NULL,
    "CalendarEventId" bigint NOT NULL,
    "Size" bigint NOT NULL,
    "Name" character varying(255) NOT NULL,
    "Extension" character varying(255) DEFAULT NULL::character varying,
    "Path" character varying(255) NOT NULL
);


ALTER TABLE public."CalendarEventAttachedFile" OWNER TO postgres;

--
-- Name: TABLE "CalendarEventAttachedFile"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON TABLE public."CalendarEventAttachedFile" IS 'Maroik.WebSite 달력 이벤트 첨부파일';


--
-- Name: COLUMN "CalendarEventAttachedFile"."Id"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarEventAttachedFile"."Id" IS 'PK';


--
-- Name: COLUMN "CalendarEventAttachedFile"."CalendarEventId"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarEventAttachedFile"."CalendarEventId" IS '부모 CalendarEvent Id';


--
-- Name: COLUMN "CalendarEventAttachedFile"."Size"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarEventAttachedFile"."Size" IS '크기 (Byte)';


--
-- Name: COLUMN "CalendarEventAttachedFile"."Name"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarEventAttachedFile"."Name" IS '이름';


--
-- Name: COLUMN "CalendarEventAttachedFile"."Extension"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarEventAttachedFile"."Extension" IS '확장자';


--
-- Name: COLUMN "CalendarEventAttachedFile"."Path"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarEventAttachedFile"."Path" IS '경로';


--
-- Name: CalendarEventAttachedFile_Id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public."CalendarEventAttachedFile_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public."CalendarEventAttachedFile_Id_seq" OWNER TO postgres;

--
-- Name: CalendarEventAttachedFile_Id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public."CalendarEventAttachedFile_Id_seq" OWNED BY public."CalendarEventAttachedFile"."Id";


--
-- Name: CalendarEventReminder; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."CalendarEventReminder" (
    "Id" bigint NOT NULL,
    "CalendarEventId" bigint NOT NULL,
    "Method" character varying(255) NOT NULL,
    "MinutesBeforeEvent" bigint,
    "HoursBeforeEvent" bigint,
    "DaysBeforeEvent" bigint,
    "WeeksBeforeEvent" bigint,
    "TimesBeforeEvent" time without time zone,
    CONSTRAINT "CalendarEventReminder_Method_check" CHECK ((("Method")::text = ANY (ARRAY[('Email'::character varying)::text, ('Notification'::character varying)::text])))
);


ALTER TABLE public."CalendarEventReminder" OWNER TO postgres;

--
-- Name: TABLE "CalendarEventReminder"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON TABLE public."CalendarEventReminder" IS 'Maroik.WebSite 달력 이벤트 알림';


--
-- Name: COLUMN "CalendarEventReminder"."Id"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarEventReminder"."Id" IS 'PK';


--
-- Name: COLUMN "CalendarEventReminder"."CalendarEventId"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarEventReminder"."CalendarEventId" IS '부모 Calendar Event Id';


--
-- Name: COLUMN "CalendarEventReminder"."Method"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarEventReminder"."Method" IS '알림 방법';


--
-- Name: COLUMN "CalendarEventReminder"."MinutesBeforeEvent"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarEventReminder"."MinutesBeforeEvent" IS '이벤트 발생 전 알림 시간 (분 단위)';


--
-- Name: COLUMN "CalendarEventReminder"."HoursBeforeEvent"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarEventReminder"."HoursBeforeEvent" IS '이벤트 발생 전 알림 시간 (시간 단위)';


--
-- Name: COLUMN "CalendarEventReminder"."DaysBeforeEvent"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarEventReminder"."DaysBeforeEvent" IS '이벤트 발생 전 알림 시간 (일 단위)';


--
-- Name: COLUMN "CalendarEventReminder"."WeeksBeforeEvent"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarEventReminder"."WeeksBeforeEvent" IS '이벤트 발생 전 알림 시간 (주 단위)';


--
-- Name: COLUMN "CalendarEventReminder"."TimesBeforeEvent"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarEventReminder"."TimesBeforeEvent" IS '이벤트 발생 전 알림 시간 (시간분 단위)';


--
-- Name: CalendarEventReminder_Id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public."CalendarEventReminder_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public."CalendarEventReminder_Id_seq" OWNER TO postgres;

--
-- Name: CalendarEventReminder_Id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public."CalendarEventReminder_Id_seq" OWNED BY public."CalendarEventReminder"."Id";


--
-- Name: CalendarEvent_Id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public."CalendarEvent_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public."CalendarEvent_Id_seq" OWNER TO postgres;

--
-- Name: CalendarEvent_Id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public."CalendarEvent_Id_seq" OWNED BY public."CalendarEvent"."Id";


--
-- Name: CalendarRecurrence; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."CalendarRecurrence" (
    "Id" bigint NOT NULL,
    "Frequency" character varying(255) NOT NULL,
    "Interval" bigint DEFAULT '1'::bigint NOT NULL,
    "DayOfWeek" character varying(255) DEFAULT NULL::character varying,
    "DayOfMonth" bigint,
    "MonthOfYear" bigint,
    "Count" bigint,
    "Until" timestamp without time zone,
    "Created" timestamp without time zone NOT NULL,
    "Updated" timestamp without time zone NOT NULL,
    CONSTRAINT "CalendarRecurrence_DayOfMonth_check" CHECK ((("DayOfMonth" >= 1) AND ("DayOfMonth" <= 31))),
    CONSTRAINT "CalendarRecurrence_DayOfWeek_check" CHECK ((("DayOfWeek" IS NULL) OR (("DayOfWeek")::text = ANY (ARRAY[('Monday'::character varying)::text, ('Tuesday'::character varying)::text, ('Wednesday'::character varying)::text, ('Thursday'::character varying)::text, ('Friday'::character varying)::text, ('Saturday'::character varying)::text, ('Sunday'::character varying)::text])))),
    CONSTRAINT "CalendarRecurrence_Frequency_check" CHECK ((("Frequency")::text = ANY (ARRAY[('DAILY'::character varying)::text, ('WEEKLY'::character varying)::text, ('MONTHLY'::character varying)::text, ('YEARLY'::character varying)::text]))),
    CONSTRAINT "CalendarRecurrence_MonthOfYear_check" CHECK ((("MonthOfYear" >= 1) AND ("MonthOfYear" <= 12)))
);


ALTER TABLE public."CalendarRecurrence" OWNER TO postgres;

--
-- Name: TABLE "CalendarRecurrence"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON TABLE public."CalendarRecurrence" IS 'Maroik.WebSite 달력 반복';


--
-- Name: COLUMN "CalendarRecurrence"."Id"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarRecurrence"."Id" IS 'PK';


--
-- Name: COLUMN "CalendarRecurrence"."Frequency"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarRecurrence"."Frequency" IS '반복 주기';


--
-- Name: COLUMN "CalendarRecurrence"."Interval"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarRecurrence"."Interval" IS '반복 간격';


--
-- Name: COLUMN "CalendarRecurrence"."DayOfWeek"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarRecurrence"."DayOfWeek" IS '반복 요일';


--
-- Name: COLUMN "CalendarRecurrence"."DayOfMonth"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarRecurrence"."DayOfMonth" IS '매월 반복일 (1~31, 옵션)';


--
-- Name: COLUMN "CalendarRecurrence"."MonthOfYear"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarRecurrence"."MonthOfYear" IS '매년 반복월 (1~12, 옵션)';


--
-- Name: COLUMN "CalendarRecurrence"."Count"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarRecurrence"."Count" IS '반복 횟수 (옵션)';


--
-- Name: COLUMN "CalendarRecurrence"."Until"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarRecurrence"."Until" IS '반복 종료 날짜 (옵션)';


--
-- Name: COLUMN "CalendarRecurrence"."Created"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarRecurrence"."Created" IS '생성일';


--
-- Name: COLUMN "CalendarRecurrence"."Updated"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarRecurrence"."Updated" IS '업데이트일';


--
-- Name: CalendarRecurrence_Id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public."CalendarRecurrence_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public."CalendarRecurrence_Id_seq" OWNER TO postgres;

--
-- Name: CalendarRecurrence_Id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public."CalendarRecurrence_Id_seq" OWNED BY public."CalendarRecurrence"."Id";


--
-- Name: CalendarShared; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."CalendarShared" (
    "CalendarId" bigint NOT NULL,
    "User" boolean NOT NULL,
    "Anonymous" boolean NOT NULL
);


ALTER TABLE public."CalendarShared" OWNER TO postgres;

--
-- Name: TABLE "CalendarShared"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON TABLE public."CalendarShared" IS 'Maroik.WebSite 달력 공유';


--
-- Name: COLUMN "CalendarShared"."CalendarId"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarShared"."CalendarId" IS '부모 Calendar Id';


--
-- Name: COLUMN "CalendarShared"."User"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarShared"."User" IS 'User 공유 여부';


--
-- Name: COLUMN "CalendarShared"."Anonymous"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarShared"."Anonymous" IS 'Anonymous 공유 여부';


--
-- Name: Calendar_Id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public."Calendar_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public."Calendar_Id_seq" OWNER TO postgres;

--
-- Name: Calendar_Id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public."Calendar_Id_seq" OWNED BY public."Calendar"."Id";


--
-- Name: Category; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."Category" (
    "Id" bigint NOT NULL,
    "Name" character varying(255) NOT NULL,
    "DisplayName" character varying(255) NOT NULL,
    "IconPath" character varying(255) NOT NULL,
    "Controller" character varying(255) NOT NULL,
    "Action" character varying(255) DEFAULT NULL::character varying,
    "Role" character varying(255) DEFAULT 'Admin'::character varying NOT NULL,
    "Order" bigint NOT NULL,
    CONSTRAINT "Category_Role_check" CHECK ((("Role")::text = ANY (ARRAY[('Admin'::character varying)::text, ('User'::character varying)::text, ('Anonymous'::character varying)::text])))
);


ALTER TABLE public."Category" OWNER TO postgres;

--
-- Name: TABLE "Category"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON TABLE public."Category" IS '카테고리 /*Maroik.WebSite의 로그인 후 접근 가능한 좌측 SideBar 설정 시 사용*/';


--
-- Name: COLUMN "Category"."Id"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Category"."Id" IS 'ID';


--
-- Name: COLUMN "Category"."Name"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Category"."Name" IS '이름';


--
-- Name: COLUMN "Category"."DisplayName"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Category"."DisplayName" IS '표시이름';


--
-- Name: COLUMN "Category"."IconPath"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Category"."IconPath" IS '표시 아이콘 경로 /*FontAwesome 사용*/';


--
-- Name: COLUMN "Category"."Controller"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Category"."Controller" IS '접근 MVC Controller 명';


--
-- Name: COLUMN "Category"."Action"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Category"."Action" IS '접근 MVC Action 명 /*이 값이 없으면 하위 카테고리 존재*/';


--
-- Name: COLUMN "Category"."Role"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Category"."Role" IS '접근 권한 설정';


--
-- Name: COLUMN "Category"."Order"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Category"."Order" IS '출력 순서';


--
-- Name: Category_Id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public."Category_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public."Category_Id_seq" OWNER TO postgres;

--
-- Name: Category_Id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public."Category_Id_seq" OWNED BY public."Category"."Id";


--
-- Name: Expenditure; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."Expenditure" (
    "Id" bigint NOT NULL,
    "AccountEmail" character varying(255) NOT NULL,
    "MainClass" character varying(255) NOT NULL,
    "SubClass" character varying(255) NOT NULL,
    "Content" character varying(255) NOT NULL,
    "Amount" numeric(18,2) NOT NULL,
    "PaymentMethod" character varying(255) NOT NULL,
    "MyDepositAsset" character varying(255) DEFAULT NULL::character varying,
    "Created" timestamp without time zone DEFAULT '1900-01-01 00:00:00'::timestamp without time zone NOT NULL,
    "Updated" timestamp without time zone DEFAULT '1900-01-01 00:00:00'::timestamp without time zone NOT NULL,
    "Note" character varying(255) DEFAULT NULL::character varying,
    CONSTRAINT "Expenditure_MainClass_check" CHECK ((("MainClass")::text = ANY (ARRAY[('RegularSavings'::character varying)::text, ('NonConsumerSpending'::character varying)::text, ('ConsumerSpending'::character varying)::text]))),
    CONSTRAINT "Expenditure_SubClass_check" CHECK ((("SubClass")::text = ANY (ARRAY[('Deposit'::character varying)::text, ('Investment'::character varying)::text, ('PublicPension'::character varying)::text, ('DebtRepayment'::character varying)::text, ('Tax'::character varying)::text, ('SocialInsurance'::character varying)::text, ('InterHouseholdTranserExpenses'::character varying)::text, ('NonProfitOrganizationTransfer'::character varying)::text, ('MealOrEatOutExpenses'::character varying)::text, ('HousingOrSuppliesCost'::character varying)::text, ('EducationExpenses'::character varying)::text, ('MedicalExpenses'::character varying)::text, ('TransportationCost'::character varying)::text, ('CommunicationCost'::character varying)::text, ('LeisureOrCulture'::character varying)::text, ('ClothingOrShoes'::character varying)::text, ('PinMoney'::character varying)::text, ('ProtectionTypeInsurance'::character varying)::text, ('OtherExpenses'::character varying)::text, ('UnknownExpenditure'::character varying)::text])))
);


ALTER TABLE public."Expenditure" OWNER TO postgres;

--
-- Name: TABLE "Expenditure"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON TABLE public."Expenditure" IS 'Maroik.WebSite 지출';


--
-- Name: COLUMN "Expenditure"."Id"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Expenditure"."Id" IS 'PK';


--
-- Name: COLUMN "Expenditure"."AccountEmail"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Expenditure"."AccountEmail" IS '계정 이메일 (ID)';


--
-- Name: COLUMN "Expenditure"."MainClass"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Expenditure"."MainClass" IS '대분류 (정기저축/비소비지출/소비지출)';


--
-- Name: COLUMN "Expenditure"."SubClass"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Expenditure"."SubClass" IS '소분류 (예적금/내자산이체/투자 | 공적연금/부채상환/세금/사회보험/가구간 이전지출/비영리단체 이전 | (식비/외식비)/(주거/용품비)/교육비/의료비/교통비/통신비/(여가/문화)/(의류/신발)/용돈/보장성보험/기타지출/미파악지출)';


--
-- Name: COLUMN "Expenditure"."Content"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Expenditure"."Content" IS '내용 (A마트/B카드/C음식점/D도서관)';


--
-- Name: COLUMN "Expenditure"."Amount"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Expenditure"."Amount" IS '금액';


--
-- Name: COLUMN "Expenditure"."PaymentMethod"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Expenditure"."PaymentMethod" IS '결제 수단 (자산 상품명/현금)';


--
-- Name: COLUMN "Expenditure"."MyDepositAsset"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Expenditure"."MyDepositAsset" IS '내 입금 자산 (자산 상품명/현금) (지출 중 [예적금, 내자산이체, 투자, 공적연금, 부채상환]일 때 사용)';


--
-- Name: COLUMN "Expenditure"."Created"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Expenditure"."Created" IS '생성일';


--
-- Name: COLUMN "Expenditure"."Updated"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Expenditure"."Updated" IS '업데이트일';


--
-- Name: COLUMN "Expenditure"."Note"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Expenditure"."Note" IS '비고';


--
-- Name: Expenditure_Id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public."Expenditure_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public."Expenditure_Id_seq" OWNER TO postgres;

--
-- Name: Expenditure_Id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public."Expenditure_Id_seq" OWNED BY public."Expenditure"."Id";


--
-- Name: FixedExpenditure; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."FixedExpenditure" (
    "Id" bigint NOT NULL,
    "AccountEmail" character varying(255) NOT NULL,
    "MainClass" character varying(255) NOT NULL,
    "SubClass" character varying(255) NOT NULL,
    "Content" character varying(255) NOT NULL,
    "Amount" numeric(18,2) NOT NULL,
    "PaymentMethod" character varying(255) NOT NULL,
    "MyDepositAsset" character varying(255) DEFAULT NULL::character varying,
    "DepositMonth" smallint NOT NULL,
    "DepositDay" smallint NOT NULL,
    "MaturityDate" timestamp without time zone DEFAULT '1900-01-01 00:00:00'::timestamp without time zone NOT NULL,
    "Created" timestamp without time zone DEFAULT '1900-01-01 00:00:00'::timestamp without time zone NOT NULL,
    "Updated" timestamp without time zone DEFAULT '1900-01-01 00:00:00'::timestamp without time zone NOT NULL,
    "Note" character varying(255) DEFAULT NULL::character varying,
    "Unpunctuality" boolean NOT NULL,
    CONSTRAINT "FixedExpenditure_DepositDay_check" CHECK ((("DepositDay" >= 1) AND ("DepositDay" <= 31))),
    CONSTRAINT "FixedExpenditure_DepositMonth_check" CHECK ((("DepositMonth" >= 1) AND ("DepositMonth" <= 12))),
    CONSTRAINT "FixedExpenditure_MainClass_check" CHECK ((("MainClass")::text = ANY (ARRAY[('RegularSavings'::character varying)::text, ('NonConsumerSpending'::character varying)::text, ('ConsumerSpending'::character varying)::text]))),
    CONSTRAINT "FixedExpenditure_SubClass_check" CHECK ((("SubClass")::text = ANY (ARRAY[('Deposit'::character varying)::text, ('Investment'::character varying)::text, ('PublicPension'::character varying)::text, ('DebtRepayment'::character varying)::text, ('Tax'::character varying)::text, ('SocialInsurance'::character varying)::text, ('InterHouseholdTranserExpenses'::character varying)::text, ('NonProfitOrganizationTransfer'::character varying)::text, ('MealOrEatOutExpenses'::character varying)::text, ('HousingOrSuppliesCost'::character varying)::text, ('EducationExpenses'::character varying)::text, ('MedicalExpenses'::character varying)::text, ('TransportationCost'::character varying)::text, ('CommunicationCost'::character varying)::text, ('LeisureOrCulture'::character varying)::text, ('ClothingOrShoes'::character varying)::text, ('PinMoney'::character varying)::text, ('ProtectionTypeInsurance'::character varying)::text, ('OtherExpenses'::character varying)::text, ('UnknownExpenditure'::character varying)::text])))
);


ALTER TABLE public."FixedExpenditure" OWNER TO postgres;

--
-- Name: TABLE "FixedExpenditure"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON TABLE public."FixedExpenditure" IS 'Maroik.WebSite 고정지출';


--
-- Name: COLUMN "FixedExpenditure"."Id"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."FixedExpenditure"."Id" IS 'PK';


--
-- Name: COLUMN "FixedExpenditure"."AccountEmail"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."FixedExpenditure"."AccountEmail" IS '계정 이메일 (ID)';


--
-- Name: COLUMN "FixedExpenditure"."MainClass"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."FixedExpenditure"."MainClass" IS '대분류 (정기저축/비소비지출/소비지출)';


--
-- Name: COLUMN "FixedExpenditure"."SubClass"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."FixedExpenditure"."SubClass" IS '소분류 (예적금/내자산이체/투자 | 공적연금/부채상환/세금/사회보험/가구간 이전지출/비영리단체 이전 | (식비/외식비)/(주거/용품비)/교육비/의료비/교통비/통신비/(여가/문화)/(의류/신발)/용돈/보장성보험/기타지출/미파악지출)';


--
-- Name: COLUMN "FixedExpenditure"."Content"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."FixedExpenditure"."Content" IS '내용 (A마트/B카드/C음식점/D도서관)';


--
-- Name: COLUMN "FixedExpenditure"."Amount"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."FixedExpenditure"."Amount" IS '금액';


--
-- Name: COLUMN "FixedExpenditure"."PaymentMethod"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."FixedExpenditure"."PaymentMethod" IS '결제 수단 (자산 상품명/현금)';


--
-- Name: COLUMN "FixedExpenditure"."MyDepositAsset"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."FixedExpenditure"."MyDepositAsset" IS '내 입금 자산 (자산 상품명/현금) (지출 중 [예적금, 내자산이체, 투자, 공적연금, 부채상환]일 때 사용)';


--
-- Name: COLUMN "FixedExpenditure"."DepositMonth"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."FixedExpenditure"."DepositMonth" IS '입금월';


--
-- Name: COLUMN "FixedExpenditure"."DepositDay"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."FixedExpenditure"."DepositDay" IS '입금일';


--
-- Name: COLUMN "FixedExpenditure"."MaturityDate"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."FixedExpenditure"."MaturityDate" IS '만기일';


--
-- Name: COLUMN "FixedExpenditure"."Created"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."FixedExpenditure"."Created" IS '생성일';


--
-- Name: COLUMN "FixedExpenditure"."Updated"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."FixedExpenditure"."Updated" IS '업데이트일';


--
-- Name: COLUMN "FixedExpenditure"."Note"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."FixedExpenditure"."Note" IS '비고';


--
-- Name: COLUMN "FixedExpenditure"."Unpunctuality"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."FixedExpenditure"."Unpunctuality" IS '시간 미엄수 (고정 지출 시간 약속을 지키지 않았을 때 계속 알림에 표시하는 용도)';


--
-- Name: FixedExpenditure_Id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public."FixedExpenditure_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public."FixedExpenditure_Id_seq" OWNER TO postgres;

--
-- Name: FixedExpenditure_Id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public."FixedExpenditure_Id_seq" OWNED BY public."FixedExpenditure"."Id";


--
-- Name: FixedIncome; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."FixedIncome" (
    "Id" bigint NOT NULL,
    "AccountEmail" character varying(255) NOT NULL,
    "MainClass" character varying(255) NOT NULL,
    "SubClass" character varying(255) NOT NULL,
    "Content" character varying(255) NOT NULL,
    "Amount" numeric(18,2) NOT NULL,
    "DepositMyAssetProductName" character varying(255) NOT NULL,
    "DepositMonth" smallint NOT NULL,
    "DepositDay" smallint NOT NULL,
    "MaturityDate" timestamp without time zone DEFAULT '1900-01-01 00:00:00'::timestamp without time zone NOT NULL,
    "Created" timestamp without time zone DEFAULT '1900-01-01 00:00:00'::timestamp without time zone NOT NULL,
    "Updated" timestamp without time zone DEFAULT '1900-01-01 00:00:00'::timestamp without time zone NOT NULL,
    "Note" character varying(255) DEFAULT NULL::character varying,
    "Unpunctuality" boolean NOT NULL,
    CONSTRAINT "FixedIncome_DepositDay_check" CHECK ((("DepositDay" >= 1) AND ("DepositDay" <= 31))),
    CONSTRAINT "FixedIncome_DepositMonth_check" CHECK ((("DepositMonth" >= 1) AND ("DepositMonth" <= 12))),
    CONSTRAINT "FixedIncome_MainClass_check" CHECK ((("MainClass")::text = ANY (ARRAY[('RegularIncome'::character varying)::text, ('IrregularIncome'::character varying)::text]))),
    CONSTRAINT "FixedIncome_SubClass_check" CHECK ((("SubClass")::text = ANY (ARRAY[('LaborIncome'::character varying)::text, ('BusinessIncome'::character varying)::text, ('PensionIncome'::character varying)::text, ('FinancialIncome'::character varying)::text, ('RentalIncome'::character varying)::text, ('OtherIncome'::character varying)::text])))
);


ALTER TABLE public."FixedIncome" OWNER TO postgres;

--
-- Name: TABLE "FixedIncome"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON TABLE public."FixedIncome" IS 'Maroik.WebSite 고정수입';


--
-- Name: COLUMN "FixedIncome"."Id"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."FixedIncome"."Id" IS 'PK';


--
-- Name: COLUMN "FixedIncome"."AccountEmail"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."FixedIncome"."AccountEmail" IS '계정 이메일 (ID)';


--
-- Name: COLUMN "FixedIncome"."MainClass"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."FixedIncome"."MainClass" IS '대분류 (정기수입/비정기수입)';


--
-- Name: COLUMN "FixedIncome"."SubClass"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."FixedIncome"."SubClass" IS '소분류 (근로수입/사업수입/연금수입/금융소득/임대수입/기타수입)';


--
-- Name: COLUMN "FixedIncome"."Content"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."FixedIncome"."Content" IS '내용 (회사명/사업명)';


--
-- Name: COLUMN "FixedIncome"."Amount"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."FixedIncome"."Amount" IS '금액';


--
-- Name: COLUMN "FixedIncome"."DepositMyAssetProductName"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."FixedIncome"."DepositMyAssetProductName" IS '입금 자산 (자산 상품명/현금)';


--
-- Name: COLUMN "FixedIncome"."DepositMonth"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."FixedIncome"."DepositMonth" IS '입금월';


--
-- Name: COLUMN "FixedIncome"."DepositDay"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."FixedIncome"."DepositDay" IS '입금일';


--
-- Name: COLUMN "FixedIncome"."MaturityDate"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."FixedIncome"."MaturityDate" IS '만기일';


--
-- Name: COLUMN "FixedIncome"."Created"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."FixedIncome"."Created" IS '생성일';


--
-- Name: COLUMN "FixedIncome"."Updated"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."FixedIncome"."Updated" IS '업데이트일';


--
-- Name: COLUMN "FixedIncome"."Note"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."FixedIncome"."Note" IS '비고';


--
-- Name: COLUMN "FixedIncome"."Unpunctuality"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."FixedIncome"."Unpunctuality" IS '시간 미엄수 (고정 수입 시간 약속을 지키지 않았을 때 계속 알림에 표시하는 용도)';


--
-- Name: FixedIncome_Id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public."FixedIncome_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public."FixedIncome_Id_seq" OWNER TO postgres;

--
-- Name: FixedIncome_Id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public."FixedIncome_Id_seq" OWNED BY public."FixedIncome"."Id";


--
-- Name: Income; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."Income" (
    "Id" bigint NOT NULL,
    "AccountEmail" character varying(255) NOT NULL,
    "MainClass" character varying(255) NOT NULL,
    "SubClass" character varying(255) NOT NULL,
    "Content" character varying(255) NOT NULL,
    "Amount" numeric(18,2) NOT NULL,
    "DepositMyAssetProductName" character varying(255) NOT NULL,
    "Created" timestamp without time zone DEFAULT '1900-01-01 00:00:00'::timestamp without time zone NOT NULL,
    "Updated" timestamp without time zone DEFAULT '1900-01-01 00:00:00'::timestamp without time zone NOT NULL,
    "Note" character varying(255) DEFAULT NULL::character varying,
    CONSTRAINT "Income_MainClass_check" CHECK ((("MainClass")::text = ANY (ARRAY[('RegularIncome'::character varying)::text, ('IrregularIncome'::character varying)::text]))),
    CONSTRAINT "Income_SubClass_check" CHECK ((("SubClass")::text = ANY (ARRAY[('LaborIncome'::character varying)::text, ('BusinessIncome'::character varying)::text, ('PensionIncome'::character varying)::text, ('FinancialIncome'::character varying)::text, ('RentalIncome'::character varying)::text, ('OtherIncome'::character varying)::text])))
);


ALTER TABLE public."Income" OWNER TO postgres;

--
-- Name: TABLE "Income"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON TABLE public."Income" IS 'Maroik.WebSite 수입';


--
-- Name: COLUMN "Income"."Id"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Income"."Id" IS 'PK';


--
-- Name: COLUMN "Income"."AccountEmail"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Income"."AccountEmail" IS '계정 이메일 (ID)';


--
-- Name: COLUMN "Income"."MainClass"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Income"."MainClass" IS '대분류 (정기수입/비정기수입)';


--
-- Name: COLUMN "Income"."SubClass"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Income"."SubClass" IS '소분류 (근로수입/사업수입/연금수입/금융소득/임대수입/기타수입)';


--
-- Name: COLUMN "Income"."Content"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Income"."Content" IS '내용 (회사명/사업명)';


--
-- Name: COLUMN "Income"."Amount"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Income"."Amount" IS '금액';


--
-- Name: COLUMN "Income"."DepositMyAssetProductName"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Income"."DepositMyAssetProductName" IS '입금 자산 (자산 상품명/현금)';


--
-- Name: COLUMN "Income"."Created"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Income"."Created" IS '생성일';


--
-- Name: COLUMN "Income"."Updated"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Income"."Updated" IS '업데이트일';


--
-- Name: COLUMN "Income"."Note"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Income"."Note" IS '비고';


--
-- Name: Income_Id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public."Income_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public."Income_Id_seq" OWNER TO postgres;

--
-- Name: Income_Id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public."Income_Id_seq" OWNED BY public."Income"."Id";


--
-- Name: OtherCalendar; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."OtherCalendar" (
    "AccountEmail" character varying(255) NOT NULL,
    "CalendarId" bigint NOT NULL
);


ALTER TABLE public."OtherCalendar" OWNER TO postgres;

--
-- Name: TABLE "OtherCalendar"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON TABLE public."OtherCalendar" IS 'Maroik.WebSite 다른 달력';


--
-- Name: COLUMN "OtherCalendar"."AccountEmail"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."OtherCalendar"."AccountEmail" IS '계정 이메일 (ID)';


--
-- Name: COLUMN "OtherCalendar"."CalendarId"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."OtherCalendar"."CalendarId" IS '부모 Calendar Id';


--
-- Name: OtherCalendar_CalendarId_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public."OtherCalendar_CalendarId_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public."OtherCalendar_CalendarId_seq" OWNER TO postgres;

--
-- Name: OtherCalendar_CalendarId_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public."OtherCalendar_CalendarId_seq" OWNED BY public."OtherCalendar"."CalendarId";


--
-- Name: SubCategory; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."SubCategory" (
    "Id" bigint NOT NULL,
    "CategoryId" bigint NOT NULL,
    "Name" character varying(255) NOT NULL,
    "DisplayName" character varying(255) NOT NULL,
    "IconPath" character varying(255) NOT NULL,
    "Action" character varying(255) NOT NULL,
    "Role" character varying(255) DEFAULT 'Admin'::character varying NOT NULL,
    "Order" bigint NOT NULL,
    CONSTRAINT "SubCategory_Role_check" CHECK (((("Role")::text = 'Admin'::text) OR (("Role")::text = 'User'::text) OR (("Role")::text = 'Anonymous'::text)))
);


ALTER TABLE public."SubCategory" OWNER TO postgres;

--
-- Name: TABLE "SubCategory"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON TABLE public."SubCategory" IS '서브 카테고리 /*Maroik.WebSite의 로그인 후 접근 가능한 좌측 SideBar 설정 시 사용*/';


--
-- Name: COLUMN "SubCategory"."Id"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."SubCategory"."Id" IS 'ID';


--
-- Name: COLUMN "SubCategory"."CategoryId"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."SubCategory"."CategoryId" IS '부모 카테고리 ID';


--
-- Name: COLUMN "SubCategory"."Name"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."SubCategory"."Name" IS '이름';


--
-- Name: COLUMN "SubCategory"."DisplayName"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."SubCategory"."DisplayName" IS '표시이름';


--
-- Name: COLUMN "SubCategory"."IconPath"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."SubCategory"."IconPath" IS '표시 아이콘 경로 /*FontAwesome 사용*/';


--
-- Name: COLUMN "SubCategory"."Action"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."SubCategory"."Action" IS '접근 MVC Action 명 /*이 값이 없으면 하위 카테고리 존재*/';


--
-- Name: COLUMN "SubCategory"."Role"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."SubCategory"."Role" IS '접근 권한 설정';


--
-- Name: COLUMN "SubCategory"."Order"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."SubCategory"."Order" IS '출력 순서';


--
-- Name: SubCategory_Id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public."SubCategory_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public."SubCategory_Id_seq" OWNER TO postgres;

--
-- Name: SubCategory_Id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public."SubCategory_Id_seq" OWNED BY public."SubCategory"."Id";


--
-- Name: Board Id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."Board" ALTER COLUMN "Id" SET DEFAULT nextval('public."Board_Id_seq"'::regclass);


--
-- Name: BoardAttachedFile Id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."BoardAttachedFile" ALTER COLUMN "Id" SET DEFAULT nextval('public."BoardAttachedFile_Id_seq"'::regclass);


--
-- Name: BoardComment Id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."BoardComment" ALTER COLUMN "Id" SET DEFAULT nextval('public."BoardComment_Id_seq"'::regclass);


--
-- Name: Calendar Id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."Calendar" ALTER COLUMN "Id" SET DEFAULT nextval('public."Calendar_Id_seq"'::regclass);


--
-- Name: CalendarEvent Id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."CalendarEvent" ALTER COLUMN "Id" SET DEFAULT nextval('public."CalendarEvent_Id_seq"'::regclass);


--
-- Name: CalendarEventAttachedFile Id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."CalendarEventAttachedFile" ALTER COLUMN "Id" SET DEFAULT nextval('public."CalendarEventAttachedFile_Id_seq"'::regclass);


--
-- Name: CalendarEventReminder Id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."CalendarEventReminder" ALTER COLUMN "Id" SET DEFAULT nextval('public."CalendarEventReminder_Id_seq"'::regclass);


--
-- Name: CalendarRecurrence Id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."CalendarRecurrence" ALTER COLUMN "Id" SET DEFAULT nextval('public."CalendarRecurrence_Id_seq"'::regclass);


--
-- Name: Category Id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."Category" ALTER COLUMN "Id" SET DEFAULT nextval('public."Category_Id_seq"'::regclass);


--
-- Name: Expenditure Id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."Expenditure" ALTER COLUMN "Id" SET DEFAULT nextval('public."Expenditure_Id_seq"'::regclass);


--
-- Name: FixedExpenditure Id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."FixedExpenditure" ALTER COLUMN "Id" SET DEFAULT nextval('public."FixedExpenditure_Id_seq"'::regclass);


--
-- Name: FixedIncome Id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."FixedIncome" ALTER COLUMN "Id" SET DEFAULT nextval('public."FixedIncome_Id_seq"'::regclass);


--
-- Name: Income Id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."Income" ALTER COLUMN "Id" SET DEFAULT nextval('public."Income_Id_seq"'::regclass);


--
-- Name: OtherCalendar CalendarId; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."OtherCalendar" ALTER COLUMN "CalendarId" SET DEFAULT nextval('public."OtherCalendar_CalendarId_seq"'::regclass);


--
-- Name: SubCategory Id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."SubCategory" ALTER COLUMN "Id" SET DEFAULT nextval('public."SubCategory_Id_seq"'::regclass);


--
-- Data for Name: Account; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public."Account" ("Email", "HashedPassword", "Nickname", "AvatarImagePath", "Role", "TimeZoneIanaId", "DefaultMonetaryUnit", "Locked", "LoginAttempt", "EmailConfirmed", "AgreedServiceTerms", "RegistrationToken", "ResetPasswordToken", "Created", "Updated", "Message", "Deleted") FROM stdin;
admin@maroik.com	R6uvPTQE5d6CXaRkYjnCxONqMIQxR5wxyuDvlmes/Tn+uYK91OtWWTo4wxOXqgzdiT1O8N2xAPxDTBluN+mDz+svw1BBB7rFdu7tVhCRxd9jbXfGBnLqk2nIzx0GQw64MfvcXwv8Kafn5SHxAw2vdZFYZk36xbeibk6GfexEclxmNB/83CAeOOddYCJDeiDEzK6mqbc/FarS/SduSgeEhZEThc2h4aQyZCtsQEsg8ej43tFfhgkXFLEAOn4qcYYure8VtQOa8c/E6r6kVZ1bDNW+WdfHyVFE26bFrQaPA/3ApegryJLfsNTUG8bFQ7MUElv5dX81rxQSztU9Ke2u0g==	Admin	/upload/Management/Profile/default-avatar.jpg	Admin	Asia/Seoul	\N	f	0	t	t	\N	\N	2021-07-12 22:37:36	2025-05-31 11:04:22.282451	Success to reset password	f
demo@maroik.com	R6uvPTQE5d6CXaRkYjnCxONqMIQxR5wxyuDvlmes/Tn+uYK91OtWWTo4wxOXqgzdiT1O8N2xAPxDTBluN+mDz+svw1BBB7rFdu7tVhCRxd9jbXfGBnLqk2nIzx0GQw64MfvcXwv8Kafn5SHxAw2vdZFYZk36xbeibk6GfexEclxmNB/83CAeOOddYCJDeiDEzK6mqbc/FarS/SduSgeEhZEThc2h4aQyZCtsQEsg8ej43tFfhgkXFLEAOn4qcYYure8VtQOa8c/E6r6kVZ1bDNW+WdfHyVFE26bFrQaPA/3ApegryJLfsNTUG8bFQ7MUElv5dX81rxQSztU9Ke2u0g==	Demo	/upload/Management/Profile/default-avatar.jpg	User	Asia/Seoul	KRW	f	0	t	t	\N	\N	2021-07-12 22:46:59	2025-05-31 11:04:51.573487	Success	f
\.


--
-- Data for Name: Asset; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public."Asset" ("ProductName", "AccountEmail", "Item", "Amount", "MonetaryUnit", "Created", "Updated", "Note", "Deleted") FROM stdin;
USA Bank	demo@maroik.com	FreeDepositAndWithdrawal	-20.00	USD	2025-05-31 09:55:56.352656	2025-05-31 10:04:38.973066		f
USA Wallet	demo@maroik.com	FreeDepositAndWithdrawal	30.00	USD	2025-05-31 09:54:32.019964	2025-05-31 10:04:38.974863		f
Korea Bank	demo@maroik.com	FreeDepositAndWithdrawal	700.00	KRW	2025-05-31 09:56:05.871871	2025-05-31 10:04:51.927684		f
Korea Wallet	demo@maroik.com	FreeDepositAndWithdrawal	2100.00	KRW	2025-05-31 09:55:14.159065	2025-05-31 10:04:51.929245		f
\.


--
-- Data for Name: Board; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public."Board" ("Id", "Type", "Title", "Content", "Writer", "Created", "Updated", "View", "Deleted", "Locked", "Noticed") FROM stdin;
110	FreeForum	자유게시판에 오신 것을 환영합니다!	<p><br></p>	Admin	2023-03-31 13:53:41.974512	2024-05-04 15:18:13.309916	4193	f	f	t
\.


--
-- Data for Name: BoardAttachedFile; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public."BoardAttachedFile" ("Id", "BoardId", "Size", "Name", "Extension", "Path") FROM stdin;
\.


--
-- Data for Name: BoardComment; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public."BoardComment" ("Id", "BoardId", "Order", "AvatarImagePath", "Writer", "Content", "Created", "Deleted") FROM stdin;
\.


--
-- Data for Name: Calendar; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public."Calendar" ("Id", "AccountEmail", "Name", "Description", "TimeZoneIanaId", "HtmlColorCode", "Created", "Updated") FROM stdin;
6	demo@maroik.com	Demo	\N	Asia/Seoul	#fc330e	2025-05-31 05:22:54.685711	2025-05-31 05:22:54.685752
7	admin@maroik.com	Admin	\N	Asia/Seoul	#fc330e	2025-05-31 11:05:23.937092	2025-05-31 11:05:23.937146
\.


--
-- Data for Name: CalendarEvent; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public."CalendarEvent" ("Id", "CalendarId", "Title", "Description", "AllDay", "StartDate", "EndDate", "StartDateTimeZoneIanaId", "EndDateTimeZoneIanaId", "Location", "Status", "RecurrenceId", "Created", "Updated") FROM stdin;
735	6	Sample		f	2025-05-31 23:00:00	2025-06-01 08:00:00	Asia/Seoul	Asia/Seoul	Location	Busy	\N	2025-05-31 09:52:07.450883	2025-05-31 09:52:07.45092
736	6	Long sample		t	2025-06-01 00:00:00	2025-06-07 00:00:00	\N	\N	Location	Busy	\N	2025-05-31 09:52:45.834435	2025-05-31 09:52:45.834435
\.


--
-- Data for Name: CalendarEventAttachedFile; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public."CalendarEventAttachedFile" ("Id", "CalendarEventId", "Size", "Name", "Extension", "Path") FROM stdin;
\.


--
-- Data for Name: CalendarEventReminder; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public."CalendarEventReminder" ("Id", "CalendarEventId", "Method", "MinutesBeforeEvent", "HoursBeforeEvent", "DaysBeforeEvent", "WeeksBeforeEvent", "TimesBeforeEvent") FROM stdin;
\.


--
-- Data for Name: CalendarRecurrence; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public."CalendarRecurrence" ("Id", "Frequency", "Interval", "DayOfWeek", "DayOfMonth", "MonthOfYear", "Count", "Until", "Created", "Updated") FROM stdin;
\.


--
-- Data for Name: CalendarShared; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public."CalendarShared" ("CalendarId", "User", "Anonymous") FROM stdin;
\.


--
-- Data for Name: Category; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public."Category" ("Id", "Name", "DisplayName", "IconPath", "Controller", "Action", "Role", "Order") FROM stdin;
1	DashBoard	DashBoard	nav-icon fas fa-tachometer-alt	DashBoard	AdminIndex	Admin	0
2	Management	Management	nav-icon fas fa-cog	Management		Admin	4
3	DashBoard	DashBoard	nav-icon fas fa-tachometer-alt	DashBoard	UserIndex	User	0
4	Management	Management	nav-icon fas fa-cog	Management		User	5
7	Develop	Develop	nav-icon fas fa-code	Develop		Admin	3
8	AccountBook	AccountBook	nav-icon fas fa-dollar-sign	AccountBook		User	4
9	Notice	Notice	nav-icon fas fa-bell	Notice		User	3
12	DashBoard	DashBoard	nav-icon fas fa-tachometer-alt	DashBoard	AnonymousIndex	Anonymous	0
16	Forum	Forum	nav-icon fas fa-comments	Forum		Admin	2
17	Forum	Forum	nav-icon fas fa-comments	Forum		User	2
18	Forum	Forum	nav-icon fas fa-comments	Forum		Anonymous	2
19	Calendar	Calendar	nav-icon far fa-calendar-alt	Calendar	AnonymousIndex	Anonymous	1
20	Calendar	Calendar	nav-icon far fa-calendar-alt	Calendar	UserIndex	User	1
21	Calendar	Calendar	nav-icon far fa-calendar-alt	Calendar	AdminIndex	Admin	1
\.


--
-- Data for Name: Expenditure; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public."Expenditure" ("Id", "AccountEmail", "MainClass", "SubClass", "Content", "Amount", "PaymentMethod", "MyDepositAsset", "Created", "Updated", "Note") FROM stdin;
5148	demo@maroik.com	RegularSavings	Deposit	Deposit	10.00	USA Bank	USA Wallet	2025-06-03 10:00:52	2025-05-31 10:04:38.965386	
5149	demo@maroik.com	ConsumerSpending	MealOrEatOutExpenses	Food	20.00	USA Bank		2025-06-11 10:00:52	2025-05-31 10:04:31.424056	
5147	demo@maroik.com	ConsumerSpending	MealOrEatOutExpenses	Food	200.00	Korea Bank		2025-06-20 10:00:52	2025-05-31 10:04:45.612039	
5146	demo@maroik.com	RegularSavings	Deposit	Deposit	100.00	Korea Bank	Korea Wallet	2025-06-25 10:00:52	2025-05-31 10:04:51.921087	
\.


--
-- Data for Name: FixedExpenditure; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public."FixedExpenditure" ("Id", "AccountEmail", "MainClass", "SubClass", "Content", "Amount", "PaymentMethod", "MyDepositAsset", "DepositMonth", "DepositDay", "MaturityDate", "Created", "Updated", "Note", "Unpunctuality") FROM stdin;
318	demo@maroik.com	ConsumerSpending	MealOrEatOutExpenses	Food	10.00	USA Wallet		1	1	9999-12-31 00:00:00	2025-05-31 09:58:36.018467	2025-05-31 09:58:36.018467		f
317	demo@maroik.com	ConsumerSpending	MealOrEatOutExpenses	Food	1000.00	Korea Wallet		1	1	9999-12-31 00:00:00	2025-05-31 09:58:29.099381	2025-05-31 09:58:40.076744		t
\.


--
-- Data for Name: FixedIncome; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public."FixedIncome" ("Id", "AccountEmail", "MainClass", "SubClass", "Content", "Amount", "DepositMyAssetProductName", "DepositMonth", "DepositDay", "MaturityDate", "Created", "Updated", "Note", "Unpunctuality") FROM stdin;
102	demo@maroik.com	RegularIncome	LaborIncome	Wage	2000000.00	Korea Bank	1	1	9999-12-31 00:00:00	2025-05-31 09:56:33.634332	2025-05-31 09:58:11.768029		t
104	demo@maroik.com	RegularIncome	PensionIncome	Wage	3000.00	Korea Wallet	1	1	9999-12-31 00:00:00	2025-05-31 09:57:07.962648	2025-05-31 09:57:07.962648		f
105	demo@maroik.com	RegularIncome	FinancialIncome	Wage	3000.00	USA Wallet	1	1	9999-12-31 00:00:00	2025-05-31 09:57:22.008801	2025-05-31 09:57:22.008801		f
103	demo@maroik.com	RegularIncome	BusinessIncome	Wage	2000.00	USA Bank	1	1	9999-12-31 00:00:00	2025-05-31 09:56:51.374288	2025-05-31 09:58:04.850587		f
\.


--
-- Data for Name: Income; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public."Income" ("Id", "AccountEmail", "MainClass", "SubClass", "Content", "Amount", "DepositMyAssetProductName", "Created", "Updated", "Note") FROM stdin;
397	demo@maroik.com	RegularIncome	LaborIncome	Wage	20.00	USA Wallet	2025-06-12 10:00:11	2025-05-31 10:04:01.11254	
396	demo@maroik.com	RegularIncome	LaborIncome	Wage	10.00	USA Bank	2025-06-17 10:00:11	2025-05-31 10:04:07.275671	
395	demo@maroik.com	RegularIncome	LaborIncome	Wage	2000.00	Korea Wallet	2025-06-09 10:00:11	2025-05-31 10:04:13.355409	
394	demo@maroik.com	RegularIncome	LaborIncome	Wage	1000.00	Korea Bank	2025-06-22 10:00:11	2025-05-31 10:04:20.48965	
\.


--
-- Data for Name: OtherCalendar; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public."OtherCalendar" ("AccountEmail", "CalendarId") FROM stdin;
\.


--
-- Data for Name: SubCategory; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public."SubCategory" ("Id", "CategoryId", "Name", "DisplayName", "IconPath", "Action", "Role", "Order") FROM stdin;
1	2	Profile	Profile	far fa-circle nav-icon	Profile	Admin	1
2	2	Account	Account	far fa-circle nav-icon	Account	Admin	2
3	2	Menu	Menu	far fa-circle nav-icon	Menu	Admin	3
4	4	Profile	Profile	far fa-circle nav-icon	Profile	User	1
11	7	API	API	far fa-circle nav-icon	API	Admin	0
12	8	Asset	Asset	far fa-circle nav-icon	Asset	User	0
14	8	Income	Income	far fa-circle nav-icon	Income	User	1
15	8	Expenditure	Expenditure	far fa-circle nav-icon	Expenditure	User	2
16	9	FixedIncome	FixedIncome	far fa-circle nav-icon	FixedIncome	User	0
17	9	FixedExpenditure	FixedExpenditure	far fa-circle nav-icon	FixedExpenditure	User	1
21	16	Free Forum	Free Forum	far fa-circle nav-icon	FreeForum	Admin	0
22	17	Free Forum	Free Forum	far fa-circle nav-icon	FreeForum	User	0
23	18	Free Forum	Free Forum	far fa-circle nav-icon	FreeForum	Anonymous	0
24	2	Private Note	Private Note	far fa-circle nav-icon	PrivateNote	Admin	0
25	4	Private Note	Private Note	far fa-circle nav-icon	PrivateNote	User	0
\.


--
-- Name: BoardAttachedFile_Id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public."BoardAttachedFile_Id_seq"', 17, true);


--
-- Name: BoardComment_Id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public."BoardComment_Id_seq"', 372, true);


--
-- Name: Board_Id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public."Board_Id_seq"', 207, true);


--
-- Name: CalendarEventAttachedFile_Id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public."CalendarEventAttachedFile_Id_seq"', 3, true);


--
-- Name: CalendarEventReminder_Id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public."CalendarEventReminder_Id_seq"', 1, true);


--
-- Name: CalendarEvent_Id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public."CalendarEvent_Id_seq"', 736, true);


--
-- Name: CalendarRecurrence_Id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public."CalendarRecurrence_Id_seq"', 1, true);


--
-- Name: Calendar_Id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public."Calendar_Id_seq"', 7, true);


--
-- Name: Category_Id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public."Category_Id_seq"', 22, false);


--
-- Name: Expenditure_Id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public."Expenditure_Id_seq"', 5149, true);


--
-- Name: FixedExpenditure_Id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public."FixedExpenditure_Id_seq"', 318, true);


--
-- Name: FixedIncome_Id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public."FixedIncome_Id_seq"', 105, true);


--
-- Name: Income_Id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public."Income_Id_seq"', 397, true);


--
-- Name: OtherCalendar_CalendarId_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public."OtherCalendar_CalendarId_seq"', 4, false);


--
-- Name: SubCategory_Id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public."SubCategory_Id_seq"', 26, false);


--
-- Name: Account Account_Nickname_unique; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."Account"
    ADD CONSTRAINT "Account_Nickname_unique" UNIQUE ("Nickname");


--
-- Name: Account Account_pk; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."Account"
    ADD CONSTRAINT "Account_pk" PRIMARY KEY ("Email");


--
-- Name: Asset Asset_pk; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."Asset"
    ADD CONSTRAINT "Asset_pk" PRIMARY KEY ("ProductName", "AccountEmail");


--
-- Name: BoardAttachedFile BoardAttachedFile_pk; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."BoardAttachedFile"
    ADD CONSTRAINT "BoardAttachedFile_pk" PRIMARY KEY ("Id");


--
-- Name: BoardComment BoardComment_pk; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."BoardComment"
    ADD CONSTRAINT "BoardComment_pk" PRIMARY KEY ("Id");


--
-- Name: Board Board_pk; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."Board"
    ADD CONSTRAINT "Board_pk" PRIMARY KEY ("Id");


--
-- Name: CalendarEventAttachedFile CalendarEventAttachedFile_pk; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."CalendarEventAttachedFile"
    ADD CONSTRAINT "CalendarEventAttachedFile_pk" PRIMARY KEY ("Id");


--
-- Name: CalendarEventReminder CalendarEventReminder_pk; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."CalendarEventReminder"
    ADD CONSTRAINT "CalendarEventReminder_pk" PRIMARY KEY ("Id");


--
-- Name: CalendarEvent CalendarEvent_pk; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."CalendarEvent"
    ADD CONSTRAINT "CalendarEvent_pk" PRIMARY KEY ("Id");


--
-- Name: CalendarRecurrence CalendarRecurrence_pk; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."CalendarRecurrence"
    ADD CONSTRAINT "CalendarRecurrence_pk" PRIMARY KEY ("Id");


--
-- Name: CalendarShared CalendarShared_pk; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."CalendarShared"
    ADD CONSTRAINT "CalendarShared_pk" PRIMARY KEY ("CalendarId");


--
-- Name: Calendar Calendar_pk; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."Calendar"
    ADD CONSTRAINT "Calendar_pk" PRIMARY KEY ("Id");


--
-- Name: Category Category_pk; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."Category"
    ADD CONSTRAINT "Category_pk" PRIMARY KEY ("Id");


--
-- Name: Expenditure Expenditure_pk; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."Expenditure"
    ADD CONSTRAINT "Expenditure_pk" PRIMARY KEY ("Id");


--
-- Name: FixedExpenditure FixedExpenditure_pk; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."FixedExpenditure"
    ADD CONSTRAINT "FixedExpenditure_pk" PRIMARY KEY ("Id");


--
-- Name: FixedIncome FixedIncome_pk; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."FixedIncome"
    ADD CONSTRAINT "FixedIncome_pk" PRIMARY KEY ("Id");


--
-- Name: Income Income_pk; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."Income"
    ADD CONSTRAINT "Income_pk" PRIMARY KEY ("Id");


--
-- Name: OtherCalendar OtherCalendar_pk; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."OtherCalendar"
    ADD CONSTRAINT "OtherCalendar_pk" PRIMARY KEY ("AccountEmail", "CalendarId");


--
-- Name: SubCategory SubCategory_pk; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."SubCategory"
    ADD CONSTRAINT "SubCategory_pk" PRIMARY KEY ("Id");


--
-- Name: Asset_index_0; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "Asset_index_0" ON public."Asset" USING btree ("AccountEmail");


--
-- Name: BoardAttachedFile_index_0; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "BoardAttachedFile_index_0" ON public."BoardAttachedFile" USING btree ("BoardId");


--
-- Name: BoardComment_index_0; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "BoardComment_index_0" ON public."BoardComment" USING btree ("BoardId");


--
-- Name: CalendarEventAttachedFile_index_0; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "CalendarEventAttachedFile_index_0" ON public."CalendarEventAttachedFile" USING btree ("CalendarEventId");


--
-- Name: CalendarEventReminder_index_0; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "CalendarEventReminder_index_0" ON public."CalendarEventReminder" USING btree ("CalendarEventId");


--
-- Name: CalendarEvent_index_0; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "CalendarEvent_index_0" ON public."CalendarEvent" USING btree ("CalendarId");


--
-- Name: CalendarEvent_index_1; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "CalendarEvent_index_1" ON public."CalendarEvent" USING btree ("RecurrenceId");


--
-- Name: CalendarShared_index_0; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "CalendarShared_index_0" ON public."CalendarShared" USING btree ("CalendarId");


--
-- Name: Calendar_index_0; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "Calendar_index_0" ON public."Calendar" USING btree ("AccountEmail");


--
-- Name: Expenditure_index_0; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "Expenditure_index_0" ON public."Expenditure" USING btree ("PaymentMethod", "AccountEmail");


--
-- Name: FixedExpenditure_index_0; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "FixedExpenditure_index_0" ON public."FixedExpenditure" USING btree ("PaymentMethod", "AccountEmail");


--
-- Name: FixedIncome_index_0; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "FixedIncome_index_0" ON public."FixedIncome" USING btree ("DepositMyAssetProductName", "AccountEmail");


--
-- Name: Income_index_0; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "Income_index_0" ON public."Income" USING btree ("DepositMyAssetProductName", "AccountEmail");


--
-- Name: OtherCalendar_index_0; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "OtherCalendar_index_0" ON public."OtherCalendar" USING btree ("AccountEmail");


--
-- Name: OtherCalendar_index_1; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "OtherCalendar_index_1" ON public."OtherCalendar" USING btree ("CalendarId");


--
-- Name: SubCategory_index_0; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "SubCategory_index_0" ON public."SubCategory" USING btree ("CategoryId");


--
-- Name: Asset Asset_fk_0; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."Asset"
    ADD CONSTRAINT "Asset_fk_0" FOREIGN KEY ("AccountEmail") REFERENCES public."Account"("Email") ON UPDATE CASCADE;


--
-- Name: BoardAttachedFile BoardAttachedFile_fk_0; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."BoardAttachedFile"
    ADD CONSTRAINT "BoardAttachedFile_fk_0" FOREIGN KEY ("BoardId") REFERENCES public."Board"("Id");


--
-- Name: BoardComment BoardComment_fk_0; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."BoardComment"
    ADD CONSTRAINT "BoardComment_fk_0" FOREIGN KEY ("BoardId") REFERENCES public."Board"("Id");


--
-- Name: CalendarEventAttachedFile CalendarEventAttachedFile_fk_0; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."CalendarEventAttachedFile"
    ADD CONSTRAINT "CalendarEventAttachedFile_fk_0" FOREIGN KEY ("CalendarEventId") REFERENCES public."CalendarEvent"("Id") ON UPDATE CASCADE ON DELETE CASCADE;


--
-- Name: CalendarEventReminder CalendarEventReminder_fk_0; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."CalendarEventReminder"
    ADD CONSTRAINT "CalendarEventReminder_fk_0" FOREIGN KEY ("CalendarEventId") REFERENCES public."CalendarEvent"("Id") ON UPDATE CASCADE ON DELETE CASCADE;


--
-- Name: CalendarEvent CalendarEvent_fk_0; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."CalendarEvent"
    ADD CONSTRAINT "CalendarEvent_fk_0" FOREIGN KEY ("CalendarId") REFERENCES public."Calendar"("Id") ON UPDATE CASCADE ON DELETE CASCADE;


--
-- Name: CalendarEvent CalendarEvent_fk_1; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."CalendarEvent"
    ADD CONSTRAINT "CalendarEvent_fk_1" FOREIGN KEY ("RecurrenceId") REFERENCES public."CalendarRecurrence"("Id") ON UPDATE CASCADE ON DELETE SET NULL;


--
-- Name: CalendarShared CalendarShared_fk_0; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."CalendarShared"
    ADD CONSTRAINT "CalendarShared_fk_0" FOREIGN KEY ("CalendarId") REFERENCES public."Calendar"("Id") ON UPDATE CASCADE ON DELETE CASCADE;


--
-- Name: Calendar Calendar_fk_0; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."Calendar"
    ADD CONSTRAINT "Calendar_fk_0" FOREIGN KEY ("AccountEmail") REFERENCES public."Account"("Email") ON UPDATE CASCADE ON DELETE CASCADE;


--
-- Name: Expenditure Expenditure_fk_0; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."Expenditure"
    ADD CONSTRAINT "Expenditure_fk_0" FOREIGN KEY ("PaymentMethod", "AccountEmail") REFERENCES public."Asset"("ProductName", "AccountEmail") ON UPDATE CASCADE;


--
-- Name: FixedExpenditure FixedExpenditure_fk_0; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."FixedExpenditure"
    ADD CONSTRAINT "FixedExpenditure_fk_0" FOREIGN KEY ("PaymentMethod", "AccountEmail") REFERENCES public."Asset"("ProductName", "AccountEmail") ON UPDATE CASCADE;


--
-- Name: FixedIncome FixedIncome_fk_0; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."FixedIncome"
    ADD CONSTRAINT "FixedIncome_fk_0" FOREIGN KEY ("DepositMyAssetProductName", "AccountEmail") REFERENCES public."Asset"("ProductName", "AccountEmail") ON UPDATE CASCADE;


--
-- Name: Income Income_fk_0; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."Income"
    ADD CONSTRAINT "Income_fk_0" FOREIGN KEY ("DepositMyAssetProductName", "AccountEmail") REFERENCES public."Asset"("ProductName", "AccountEmail") ON UPDATE CASCADE;


--
-- Name: OtherCalendar OtherCalendar_fk_0; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."OtherCalendar"
    ADD CONSTRAINT "OtherCalendar_fk_0" FOREIGN KEY ("AccountEmail") REFERENCES public."Account"("Email") ON UPDATE CASCADE ON DELETE CASCADE;


--
-- Name: OtherCalendar OtherCalendar_fk_1; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."OtherCalendar"
    ADD CONSTRAINT "OtherCalendar_fk_1" FOREIGN KEY ("CalendarId") REFERENCES public."Calendar"("Id") ON UPDATE CASCADE ON DELETE CASCADE;


--
-- Name: SubCategory SubCategory_fk_0; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."SubCategory"
    ADD CONSTRAINT "SubCategory_fk_0" FOREIGN KEY ("CategoryId") REFERENCES public."Category"("Id") ON UPDATE CASCADE ON DELETE CASCADE;


--
-- Name: SCHEMA public; Type: ACL; Schema: -; Owner: postgres
--

REVOKE USAGE ON SCHEMA public FROM PUBLIC;
GRANT CREATE ON SCHEMA public TO PUBLIC;


--
-- PostgreSQL database dump complete
--

