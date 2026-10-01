--
-- PostgreSQL database dump
--

\restrict 1PZgDvTs1dJDSEjIBAFAWXcty99zpjjvh78z1tw18GGhJoYb92e6eLbOUdSeS6H

-- Dumped from database version 17.11 (Debian 17.11-1.pgdg13+2)
-- Dumped by pg_dump version 17.11 (Debian 17.11-1.pgdg13+2)

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
    "Created" timestamp without time zone DEFAULT now() NOT NULL,
    "Updated" timestamp without time zone DEFAULT now() NOT NULL,
    "Message" text,
    "Deleted" boolean NOT NULL,
    "SecurityStamp" text DEFAULT (gen_random_uuid())::text NOT NULL,
    "MustChangePassword" boolean DEFAULT false NOT NULL,
    CONSTRAINT "Account_Email_check" CHECK ((("Email")::text = lower(("Email")::text))),
    CONSTRAINT "Account_LoginAttempt_check" CHECK (("LoginAttempt" >= 0)),
    CONSTRAINT "Account_Role_check" CHECK (((("Role")::text = 'Admin'::text) OR (("Role")::text = 'User'::text)))
);


ALTER TABLE public."Account" OWNER TO postgres;

--
-- Name: TABLE "Account"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON TABLE public."Account" IS 'Account';


--
-- Name: COLUMN "Account"."Email"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Account"."Email" IS 'Email (ID)';


--
-- Name: COLUMN "Account"."HashedPassword"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Account"."HashedPassword" IS 'HashedPassword';


--
-- Name: COLUMN "Account"."Nickname"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Account"."Nickname" IS 'Nickname';


--
-- Name: COLUMN "Account"."AvatarImagePath"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Account"."AvatarImagePath" IS 'AvatarImagePath';


--
-- Name: COLUMN "Account"."Role"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Account"."Role" IS 'Role (Admin or User)';


--
-- Name: COLUMN "Account"."TimeZoneIanaId"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Account"."TimeZoneIanaId" IS 'IANA TimeZone ID';


--
-- Name: COLUMN "Account"."DefaultMonetaryUnit"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Account"."DefaultMonetaryUnit" IS 'Default monetary unit (KRW, USD, ETC)';


--
-- Name: COLUMN "Account"."Locked"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Account"."Locked" IS 'Locked';


--
-- Name: COLUMN "Account"."LoginAttempt"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Account"."LoginAttempt" IS 'LoginAttempt';


--
-- Name: COLUMN "Account"."EmailConfirmed"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Account"."EmailConfirmed" IS 'EmailConfirmed';


--
-- Name: COLUMN "Account"."AgreedServiceTerms"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Account"."AgreedServiceTerms" IS 'AgreedServiceTerms';


--
-- Name: COLUMN "Account"."RegistrationToken"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Account"."RegistrationToken" IS 'RegistrationToken';


--
-- Name: COLUMN "Account"."ResetPasswordToken"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Account"."ResetPasswordToken" IS 'ResetPasswordToken';


--
-- Name: COLUMN "Account"."Created"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Account"."Created" IS 'Created';


--
-- Name: COLUMN "Account"."Updated"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Account"."Updated" IS 'Updated';


--
-- Name: COLUMN "Account"."Message"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Account"."Message" IS 'Message';


--
-- Name: COLUMN "Account"."Deleted"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Account"."Deleted" IS 'Deleted';


--
-- Name: COLUMN "Account"."SecurityStamp"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Account"."SecurityStamp" IS 'Opaque value that changes on every password change; invalidates other open sessions';


--
-- Name: COLUMN "Account"."MustChangePassword"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Account"."MustChangePassword" IS 'Forces a password change on next login (set by an admin password override)';


--
-- Name: Asset; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."Asset" (
    "ProductName" character varying(255) NOT NULL,
    "AccountEmail" character varying(255) NOT NULL,
    "Item" character varying(255) NOT NULL,
    "Amount" numeric(20,4) NOT NULL,
    "MonetaryUnit" character varying(45) NOT NULL,
    "Created" timestamp without time zone NOT NULL,
    "Updated" timestamp without time zone NOT NULL,
    "Note" character varying(255) DEFAULT ''::character varying NOT NULL,
    "Deleted" boolean NOT NULL,
    CONSTRAINT "Asset_Item_check" CHECK ((("Item")::text = ANY (ARRAY[('FreeDepositAndWithdrawal'::character varying)::text, ('TrustAsset'::character varying)::text, ('CashAsset'::character varying)::text, ('SavingsAsset'::character varying)::text, ('InvestmentAsset'::character varying)::text, ('RealEstate'::character varying)::text, ('Movables'::character varying)::text, ('OtherPhysicalAsset'::character varying)::text, ('InsuranceAsset'::character varying)::text]))),
    CONSTRAINT "Asset_ProductName_check" CHECK ((("ProductName")::text ~ '\S'::text))
);


ALTER TABLE public."Asset" OWNER TO postgres;

--
-- Name: TABLE "Asset"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON TABLE public."Asset" IS 'Asset';


--
-- Name: COLUMN "Asset"."ProductName"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Asset"."ProductName" IS 'ProductName';


--
-- Name: COLUMN "Asset"."AccountEmail"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Asset"."AccountEmail" IS 'AccountEmail (ID)';


--
-- Name: COLUMN "Asset"."Item"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Asset"."Item" IS 'Item';


--
-- Name: COLUMN "Asset"."Amount"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Asset"."Amount" IS 'Amount';


--
-- Name: COLUMN "Asset"."MonetaryUnit"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Asset"."MonetaryUnit" IS 'MonetaryUnit (KRW, USD, ETC)';


--
-- Name: COLUMN "Asset"."Created"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Asset"."Created" IS 'Created';


--
-- Name: COLUMN "Asset"."Updated"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Asset"."Updated" IS 'Updated';


--
-- Name: COLUMN "Asset"."Note"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Asset"."Note" IS 'Note';


--
-- Name: COLUMN "Asset"."Deleted"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Asset"."Deleted" IS 'Deleted';


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
    CONSTRAINT "Board_Title_check" CHECK ((("Title")::text ~ '\S'::text)),
    CONSTRAINT "Board_Type_check" CHECK ((("Type")::text = ANY (ARRAY[('FreeForum'::character varying)::text, ('PrivateNote'::character varying)::text]))),
    CONSTRAINT "Board_View_check" CHECK (("View" >= 0))
);


ALTER TABLE public."Board" OWNER TO postgres;

--
-- Name: TABLE "Board"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON TABLE public."Board" IS 'Board';


--
-- Name: COLUMN "Board"."Id"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Board"."Id" IS 'PK';


--
-- Name: COLUMN "Board"."Type"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Board"."Type" IS 'Type';


--
-- Name: COLUMN "Board"."Title"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Board"."Title" IS 'Title';


--
-- Name: COLUMN "Board"."Content"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Board"."Content" IS 'Content';


--
-- Name: COLUMN "Board"."Writer"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Board"."Writer" IS 'Writer';


--
-- Name: COLUMN "Board"."Created"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Board"."Created" IS 'Created';


--
-- Name: COLUMN "Board"."Updated"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Board"."Updated" IS 'Updated';


--
-- Name: COLUMN "Board"."View"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Board"."View" IS 'View';


--
-- Name: COLUMN "Board"."Deleted"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Board"."Deleted" IS 'Deleted';


--
-- Name: COLUMN "Board"."Locked"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Board"."Locked" IS 'Locked';


--
-- Name: COLUMN "Board"."Noticed"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Board"."Noticed" IS 'Noticed';


--
-- Name: BoardAttachedFile; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."BoardAttachedFile" (
    "Id" bigint NOT NULL,
    "BoardId" bigint NOT NULL,
    "Size" bigint NOT NULL,
    "Name" character varying(255) NOT NULL,
    "Extension" character varying(255) DEFAULT NULL::character varying,
    "Path" character varying(255) NOT NULL,
    CONSTRAINT "BoardAttachedFile_Size_check" CHECK (("Size" >= 0))
);


ALTER TABLE public."BoardAttachedFile" OWNER TO postgres;

--
-- Name: TABLE "BoardAttachedFile"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON TABLE public."BoardAttachedFile" IS 'BoardAttachedFile';


--
-- Name: COLUMN "BoardAttachedFile"."Id"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."BoardAttachedFile"."Id" IS 'PK';


--
-- Name: COLUMN "BoardAttachedFile"."BoardId"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."BoardAttachedFile"."BoardId" IS 'Parent Board Id';


--
-- Name: COLUMN "BoardAttachedFile"."Size"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."BoardAttachedFile"."Size" IS 'Size (Byte)';


--
-- Name: COLUMN "BoardAttachedFile"."Name"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."BoardAttachedFile"."Name" IS 'Name';


--
-- Name: COLUMN "BoardAttachedFile"."Extension"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."BoardAttachedFile"."Extension" IS 'Extension';


--
-- Name: COLUMN "BoardAttachedFile"."Path"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."BoardAttachedFile"."Path" IS 'Path';


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
    "Deleted" boolean NOT NULL,
    CONSTRAINT "BoardComment_Order_check" CHECK (("Order" >= 0))
);


ALTER TABLE public."BoardComment" OWNER TO postgres;

--
-- Name: TABLE "BoardComment"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON TABLE public."BoardComment" IS 'BoardComment';


--
-- Name: COLUMN "BoardComment"."Id"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."BoardComment"."Id" IS 'PK';


--
-- Name: COLUMN "BoardComment"."BoardId"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."BoardComment"."BoardId" IS 'Board Id';


--
-- Name: COLUMN "BoardComment"."Order"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."BoardComment"."Order" IS 'Order';


--
-- Name: COLUMN "BoardComment"."AvatarImagePath"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."BoardComment"."AvatarImagePath" IS 'AvatarImagePath';


--
-- Name: COLUMN "BoardComment"."Writer"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."BoardComment"."Writer" IS 'Writer';


--
-- Name: COLUMN "BoardComment"."Content"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."BoardComment"."Content" IS 'Content';


--
-- Name: COLUMN "BoardComment"."Created"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."BoardComment"."Created" IS 'Created';


--
-- Name: COLUMN "BoardComment"."Deleted"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."BoardComment"."Deleted" IS 'Deleted';


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
    "Updated" timestamp without time zone NOT NULL,
    CONSTRAINT "Calendar_HtmlColorCode_check" CHECK ((("HtmlColorCode")::text ~ '^#[0-9A-Fa-f]{6}$'::text)),
    CONSTRAINT "Calendar_Name_check" CHECK ((("Name")::text ~ '\S'::text))
);


ALTER TABLE public."Calendar" OWNER TO postgres;

--
-- Name: TABLE "Calendar"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON TABLE public."Calendar" IS 'Calendar';


--
-- Name: COLUMN "Calendar"."Id"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Calendar"."Id" IS 'PK';


--
-- Name: COLUMN "Calendar"."AccountEmail"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Calendar"."AccountEmail" IS 'AccountEmail (ID)';


--
-- Name: COLUMN "Calendar"."Name"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Calendar"."Name" IS 'Name';


--
-- Name: COLUMN "Calendar"."Description"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Calendar"."Description" IS 'Description';


--
-- Name: COLUMN "Calendar"."TimeZoneIanaId"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Calendar"."TimeZoneIanaId" IS 'IANA TimeZone ID';


--
-- Name: COLUMN "Calendar"."HtmlColorCode"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Calendar"."HtmlColorCode" IS 'HtmlColorCode';


--
-- Name: COLUMN "Calendar"."Created"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Calendar"."Created" IS 'Created';


--
-- Name: COLUMN "Calendar"."Updated"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Calendar"."Updated" IS 'Updated';


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
    CONSTRAINT "CalendarEvent_EndDate_check" CHECK (("EndDate" >= "StartDate")),
    CONSTRAINT "CalendarEvent_Status_check" CHECK ((("Status")::text = ANY (ARRAY[('Busy'::character varying)::text, ('Free'::character varying)::text]))),
    CONSTRAINT "CalendarEvent_Title_check" CHECK ((("Title")::text ~ '\S'::text))
);


ALTER TABLE public."CalendarEvent" OWNER TO postgres;

--
-- Name: TABLE "CalendarEvent"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON TABLE public."CalendarEvent" IS 'CalendarEvent';


--
-- Name: COLUMN "CalendarEvent"."Id"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarEvent"."Id" IS 'PK';


--
-- Name: COLUMN "CalendarEvent"."CalendarId"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarEvent"."CalendarId" IS 'Parent Calendar Id';


--
-- Name: COLUMN "CalendarEvent"."Title"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarEvent"."Title" IS 'Title';


--
-- Name: COLUMN "CalendarEvent"."Description"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarEvent"."Description" IS 'Description';


--
-- Name: COLUMN "CalendarEvent"."AllDay"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarEvent"."AllDay" IS 'AllDay';


--
-- Name: COLUMN "CalendarEvent"."StartDate"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarEvent"."StartDate" IS 'StartDate';


--
-- Name: COLUMN "CalendarEvent"."EndDate"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarEvent"."EndDate" IS 'EndDate';


--
-- Name: COLUMN "CalendarEvent"."StartDateTimeZoneIanaId"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarEvent"."StartDateTimeZoneIanaId" IS 'StartDateTimeZone (IANA TimeZone ID)';


--
-- Name: COLUMN "CalendarEvent"."EndDateTimeZoneIanaId"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarEvent"."EndDateTimeZoneIanaId" IS 'EndDateTimeZone (IANA TimeZone ID)';


--
-- Name: COLUMN "CalendarEvent"."Location"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarEvent"."Location" IS 'Location';


--
-- Name: COLUMN "CalendarEvent"."Status"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarEvent"."Status" IS 'Status';


--
-- Name: COLUMN "CalendarEvent"."RecurrenceId"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarEvent"."RecurrenceId" IS 'Recurrence ID (Option)';


--
-- Name: COLUMN "CalendarEvent"."Created"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarEvent"."Created" IS 'Created';


--
-- Name: COLUMN "CalendarEvent"."Updated"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarEvent"."Updated" IS 'Updated';


--
-- Name: CalendarEventAttachedFile; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public."CalendarEventAttachedFile" (
    "Id" bigint NOT NULL,
    "CalendarEventId" bigint NOT NULL,
    "Size" bigint NOT NULL,
    "Name" character varying(255) NOT NULL,
    "Extension" character varying(255) DEFAULT NULL::character varying,
    "Path" character varying(255) NOT NULL,
    CONSTRAINT "CalendarEventAttachedFile_Size_check" CHECK (("Size" >= 0))
);


ALTER TABLE public."CalendarEventAttachedFile" OWNER TO postgres;

--
-- Name: TABLE "CalendarEventAttachedFile"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON TABLE public."CalendarEventAttachedFile" IS 'CalendarEventAttachedFile';


--
-- Name: COLUMN "CalendarEventAttachedFile"."Id"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarEventAttachedFile"."Id" IS 'PK';


--
-- Name: COLUMN "CalendarEventAttachedFile"."CalendarEventId"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarEventAttachedFile"."CalendarEventId" IS 'Parent CalendarEvent Id';


--
-- Name: COLUMN "CalendarEventAttachedFile"."Size"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarEventAttachedFile"."Size" IS 'Size (Byte)';


--
-- Name: COLUMN "CalendarEventAttachedFile"."Name"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarEventAttachedFile"."Name" IS 'Name';


--
-- Name: COLUMN "CalendarEventAttachedFile"."Extension"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarEventAttachedFile"."Extension" IS 'Extension';


--
-- Name: COLUMN "CalendarEventAttachedFile"."Path"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarEventAttachedFile"."Path" IS 'Path';


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
    CONSTRAINT "CalendarEventReminder_BeforeEvent_check" CHECK ((num_nonnulls("MinutesBeforeEvent", "HoursBeforeEvent", "DaysBeforeEvent", "WeeksBeforeEvent") = 1)),
    CONSTRAINT "CalendarEventReminder_DaysBeforeEvent_check" CHECK ((("DaysBeforeEvent" IS NULL) OR (("DaysBeforeEvent" >= 0) AND ("DaysBeforeEvent" <= 28)))),
    CONSTRAINT "CalendarEventReminder_HoursBeforeEvent_check" CHECK ((("HoursBeforeEvent" IS NULL) OR (("HoursBeforeEvent" >= 0) AND ("HoursBeforeEvent" <= 672)))),
    CONSTRAINT "CalendarEventReminder_Method_check" CHECK ((("Method")::text = ANY (ARRAY[('Email'::character varying)::text, ('Notification'::character varying)::text]))),
    CONSTRAINT "CalendarEventReminder_MinutesBeforeEvent_check" CHECK ((("MinutesBeforeEvent" IS NULL) OR (("MinutesBeforeEvent" >= 0) AND ("MinutesBeforeEvent" <= 40320)))),
    CONSTRAINT "CalendarEventReminder_WeeksBeforeEvent_check" CHECK ((("WeeksBeforeEvent" IS NULL) OR (("WeeksBeforeEvent" >= 0) AND ("WeeksBeforeEvent" <= 4))))
);


ALTER TABLE public."CalendarEventReminder" OWNER TO postgres;

--
-- Name: TABLE "CalendarEventReminder"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON TABLE public."CalendarEventReminder" IS 'CalendarEventReminder';


--
-- Name: COLUMN "CalendarEventReminder"."Id"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarEventReminder"."Id" IS 'PK';


--
-- Name: COLUMN "CalendarEventReminder"."CalendarEventId"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarEventReminder"."CalendarEventId" IS 'Parent Calendar Event Id';


--
-- Name: COLUMN "CalendarEventReminder"."Method"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarEventReminder"."Method" IS 'Method';


--
-- Name: COLUMN "CalendarEventReminder"."MinutesBeforeEvent"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarEventReminder"."MinutesBeforeEvent" IS 'MinutesBeforeEvent';


--
-- Name: COLUMN "CalendarEventReminder"."HoursBeforeEvent"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarEventReminder"."HoursBeforeEvent" IS 'HoursBeforeEvent';


--
-- Name: COLUMN "CalendarEventReminder"."DaysBeforeEvent"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarEventReminder"."DaysBeforeEvent" IS 'DaysBeforeEvent';


--
-- Name: COLUMN "CalendarEventReminder"."WeeksBeforeEvent"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarEventReminder"."WeeksBeforeEvent" IS 'WeeksBeforeEvent';


--
-- Name: COLUMN "CalendarEventReminder"."TimesBeforeEvent"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarEventReminder"."TimesBeforeEvent" IS 'TimesBeforeEvent';


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
    CONSTRAINT "CalendarRecurrence_Count_check" CHECK ((("Count" IS NULL) OR ("Count" >= 1))),
    CONSTRAINT "CalendarRecurrence_DayOfMonth_check" CHECK ((("DayOfMonth" >= 1) AND ("DayOfMonth" <= 31))),
    CONSTRAINT "CalendarRecurrence_DayOfWeek_check" CHECK ((("DayOfWeek" IS NULL) OR (("DayOfWeek")::text = ANY (ARRAY[('Monday'::character varying)::text, ('Tuesday'::character varying)::text, ('Wednesday'::character varying)::text, ('Thursday'::character varying)::text, ('Friday'::character varying)::text, ('Saturday'::character varying)::text, ('Sunday'::character varying)::text])))),
    CONSTRAINT "CalendarRecurrence_Frequency_check" CHECK ((("Frequency")::text = ANY (ARRAY[('DAILY'::character varying)::text, ('WEEKLY'::character varying)::text, ('MONTHLY'::character varying)::text, ('YEARLY'::character varying)::text]))),
    CONSTRAINT "CalendarRecurrence_Interval_check" CHECK (("Interval" >= 1)),
    CONSTRAINT "CalendarRecurrence_MonthOfYear_check" CHECK ((("MonthOfYear" >= 1) AND ("MonthOfYear" <= 12)))
);


ALTER TABLE public."CalendarRecurrence" OWNER TO postgres;

--
-- Name: TABLE "CalendarRecurrence"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON TABLE public."CalendarRecurrence" IS 'CalendarRecurrence';


--
-- Name: COLUMN "CalendarRecurrence"."Id"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarRecurrence"."Id" IS 'PK';


--
-- Name: COLUMN "CalendarRecurrence"."Frequency"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarRecurrence"."Frequency" IS 'Frequency';


--
-- Name: COLUMN "CalendarRecurrence"."Interval"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarRecurrence"."Interval" IS 'Interval';


--
-- Name: COLUMN "CalendarRecurrence"."DayOfWeek"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarRecurrence"."DayOfWeek" IS 'DayOfWeek';


--
-- Name: COLUMN "CalendarRecurrence"."DayOfMonth"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarRecurrence"."DayOfMonth" IS 'DayOfMonth';


--
-- Name: COLUMN "CalendarRecurrence"."MonthOfYear"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarRecurrence"."MonthOfYear" IS 'MonthOfYear';


--
-- Name: COLUMN "CalendarRecurrence"."Count"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarRecurrence"."Count" IS 'Count';


--
-- Name: COLUMN "CalendarRecurrence"."Until"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarRecurrence"."Until" IS 'Until';


--
-- Name: COLUMN "CalendarRecurrence"."Created"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarRecurrence"."Created" IS 'Created';


--
-- Name: COLUMN "CalendarRecurrence"."Updated"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarRecurrence"."Updated" IS 'Updated';


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

COMMENT ON TABLE public."CalendarShared" IS 'CalendarShared';


--
-- Name: COLUMN "CalendarShared"."CalendarId"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarShared"."CalendarId" IS 'Parent Calendar Id';


--
-- Name: COLUMN "CalendarShared"."User"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarShared"."User" IS 'Is user shared';


--
-- Name: COLUMN "CalendarShared"."Anonymous"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."CalendarShared"."Anonymous" IS 'Is anonymous shared';


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
    CONSTRAINT "Category_Order_check" CHECK (("Order" >= 0)),
    CONSTRAINT "Category_Role_check" CHECK ((("Role")::text = ANY (ARRAY[('Admin'::character varying)::text, ('User'::character varying)::text, ('Anonymous'::character varying)::text])))
);


ALTER TABLE public."Category" OWNER TO postgres;

--
-- Name: TABLE "Category"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON TABLE public."Category" IS 'Category';


--
-- Name: COLUMN "Category"."Id"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Category"."Id" IS 'ID';


--
-- Name: COLUMN "Category"."Name"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Category"."Name" IS 'Name';


--
-- Name: COLUMN "Category"."DisplayName"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Category"."DisplayName" IS 'DisplayName';


--
-- Name: COLUMN "Category"."IconPath"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Category"."IconPath" IS 'IconPath';


--
-- Name: COLUMN "Category"."Controller"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Category"."Controller" IS 'Controller';


--
-- Name: COLUMN "Category"."Action"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Category"."Action" IS 'Action';


--
-- Name: COLUMN "Category"."Role"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Category"."Role" IS 'Role';


--
-- Name: COLUMN "Category"."Order"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Category"."Order" IS 'Order';


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
    "Amount" numeric(20,4) NOT NULL,
    "PaymentMethod" character varying(255) NOT NULL,
    "MyDepositAsset" character varying(255) DEFAULT NULL::character varying,
    "Created" timestamp without time zone DEFAULT now() NOT NULL,
    "Updated" timestamp without time zone DEFAULT now() NOT NULL,
    "Note" character varying(255) DEFAULT ''::character varying NOT NULL,
    CONSTRAINT "Expenditure_Content_check" CHECK ((("Content")::text ~ '\S'::text)),
    CONSTRAINT "Expenditure_MainClass_check" CHECK ((("MainClass")::text = ANY (ARRAY[('RegularSavings'::character varying)::text, ('NonConsumerSpending'::character varying)::text, ('ConsumerSpending'::character varying)::text]))),
    CONSTRAINT "Expenditure_SubClass_check" CHECK ((("SubClass")::text = ANY (ARRAY[('Deposit'::character varying)::text, ('Investment'::character varying)::text, ('PublicPension'::character varying)::text, ('DebtRepayment'::character varying)::text, ('Tax'::character varying)::text, ('SocialInsurance'::character varying)::text, ('InterHouseholdTransferExpenses'::character varying)::text, ('NonProfitOrganizationTransfer'::character varying)::text, ('MealOrEatOutExpenses'::character varying)::text, ('HousingOrSuppliesCost'::character varying)::text, ('EducationExpenses'::character varying)::text, ('MedicalExpenses'::character varying)::text, ('TransportationCost'::character varying)::text, ('CommunicationCost'::character varying)::text, ('LeisureOrCulture'::character varying)::text, ('ClothingOrShoes'::character varying)::text, ('PinMoney'::character varying)::text, ('ProtectionTypeInsurance'::character varying)::text, ('OtherExpenses'::character varying)::text, ('UnknownExpenditure'::character varying)::text])))
);


ALTER TABLE public."Expenditure" OWNER TO postgres;

--
-- Name: TABLE "Expenditure"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON TABLE public."Expenditure" IS 'Expenditure';


--
-- Name: COLUMN "Expenditure"."Id"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Expenditure"."Id" IS 'PK';


--
-- Name: COLUMN "Expenditure"."AccountEmail"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Expenditure"."AccountEmail" IS 'Account Email (ID)';


--
-- Name: COLUMN "Expenditure"."MainClass"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Expenditure"."MainClass" IS 'MainClass';


--
-- Name: COLUMN "Expenditure"."SubClass"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Expenditure"."SubClass" IS 'SubClass';


--
-- Name: COLUMN "Expenditure"."Content"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Expenditure"."Content" IS 'Content';


--
-- Name: COLUMN "Expenditure"."Amount"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Expenditure"."Amount" IS 'Amount';


--
-- Name: COLUMN "Expenditure"."PaymentMethod"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Expenditure"."PaymentMethod" IS 'PaymentMethod';


--
-- Name: COLUMN "Expenditure"."MyDepositAsset"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Expenditure"."MyDepositAsset" IS 'MyDepositAsset';


--
-- Name: COLUMN "Expenditure"."Created"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Expenditure"."Created" IS 'Created';


--
-- Name: COLUMN "Expenditure"."Updated"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Expenditure"."Updated" IS 'Updated';


--
-- Name: COLUMN "Expenditure"."Note"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Expenditure"."Note" IS 'Note';


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
    "Amount" numeric(20,4) NOT NULL,
    "PaymentMethod" character varying(255) NOT NULL,
    "MyDepositAsset" character varying(255) DEFAULT NULL::character varying,
    "DepositMonth" smallint NOT NULL,
    "DepositDay" smallint NOT NULL,
    "MaturityDate" timestamp without time zone DEFAULT now() NOT NULL,
    "Created" timestamp without time zone DEFAULT now() NOT NULL,
    "Updated" timestamp without time zone DEFAULT now() NOT NULL,
    "Note" character varying(255) DEFAULT ''::character varying NOT NULL,
    "Unpunctuality" boolean NOT NULL,
    CONSTRAINT "FixedExpenditure_DepositDayMonth_check" CHECK (((("DepositMonth" = ANY (ARRAY[1, 3, 5, 7, 8, 10, 12])) AND (("DepositDay" >= 1) AND ("DepositDay" <= 31))) OR (("DepositMonth" = ANY (ARRAY[4, 6, 9, 11])) AND (("DepositDay" >= 1) AND ("DepositDay" <= 30))) OR (("DepositMonth" = 2) AND (("DepositDay" >= 1) AND ("DepositDay" <= 29))))),
    CONSTRAINT "FixedExpenditure_MainClass_check" CHECK ((("MainClass")::text = ANY (ARRAY[('RegularSavings'::character varying)::text, ('NonConsumerSpending'::character varying)::text, ('ConsumerSpending'::character varying)::text]))),
    CONSTRAINT "FixedExpenditure_SubClass_check" CHECK ((("SubClass")::text = ANY (ARRAY[('Deposit'::character varying)::text, ('Investment'::character varying)::text, ('PublicPension'::character varying)::text, ('DebtRepayment'::character varying)::text, ('Tax'::character varying)::text, ('SocialInsurance'::character varying)::text, ('InterHouseholdTransferExpenses'::character varying)::text, ('NonProfitOrganizationTransfer'::character varying)::text, ('MealOrEatOutExpenses'::character varying)::text, ('HousingOrSuppliesCost'::character varying)::text, ('EducationExpenses'::character varying)::text, ('MedicalExpenses'::character varying)::text, ('TransportationCost'::character varying)::text, ('CommunicationCost'::character varying)::text, ('LeisureOrCulture'::character varying)::text, ('ClothingOrShoes'::character varying)::text, ('PinMoney'::character varying)::text, ('ProtectionTypeInsurance'::character varying)::text, ('OtherExpenses'::character varying)::text, ('UnknownExpenditure'::character varying)::text])))
);


ALTER TABLE public."FixedExpenditure" OWNER TO postgres;

--
-- Name: TABLE "FixedExpenditure"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON TABLE public."FixedExpenditure" IS 'FixedExpenditure';


--
-- Name: COLUMN "FixedExpenditure"."Id"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."FixedExpenditure"."Id" IS 'PK';


--
-- Name: COLUMN "FixedExpenditure"."AccountEmail"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."FixedExpenditure"."AccountEmail" IS 'Account Email (ID)';


--
-- Name: COLUMN "FixedExpenditure"."MainClass"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."FixedExpenditure"."MainClass" IS 'MainClass';


--
-- Name: COLUMN "FixedExpenditure"."SubClass"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."FixedExpenditure"."SubClass" IS 'SubClass';


--
-- Name: COLUMN "FixedExpenditure"."Content"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."FixedExpenditure"."Content" IS 'Content';


--
-- Name: COLUMN "FixedExpenditure"."Amount"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."FixedExpenditure"."Amount" IS 'Amount';


--
-- Name: COLUMN "FixedExpenditure"."PaymentMethod"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."FixedExpenditure"."PaymentMethod" IS 'PaymentMethod';


--
-- Name: COLUMN "FixedExpenditure"."MyDepositAsset"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."FixedExpenditure"."MyDepositAsset" IS 'MyDepositAsset';


--
-- Name: COLUMN "FixedExpenditure"."DepositMonth"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."FixedExpenditure"."DepositMonth" IS 'DepositMonth';


--
-- Name: COLUMN "FixedExpenditure"."DepositDay"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."FixedExpenditure"."DepositDay" IS 'DepositDay';


--
-- Name: COLUMN "FixedExpenditure"."MaturityDate"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."FixedExpenditure"."MaturityDate" IS 'MaturityDate';


--
-- Name: COLUMN "FixedExpenditure"."Created"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."FixedExpenditure"."Created" IS 'Created';


--
-- Name: COLUMN "FixedExpenditure"."Updated"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."FixedExpenditure"."Updated" IS 'Updated';


--
-- Name: COLUMN "FixedExpenditure"."Note"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."FixedExpenditure"."Note" IS 'Note';


--
-- Name: COLUMN "FixedExpenditure"."Unpunctuality"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."FixedExpenditure"."Unpunctuality" IS 'Unpunctuality';


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
    "Amount" numeric(20,4) NOT NULL,
    "DepositMyAssetProductName" character varying(255) NOT NULL,
    "DepositMonth" smallint NOT NULL,
    "DepositDay" smallint NOT NULL,
    "MaturityDate" timestamp without time zone DEFAULT now() NOT NULL,
    "Created" timestamp without time zone DEFAULT now() NOT NULL,
    "Updated" timestamp without time zone DEFAULT now() NOT NULL,
    "Note" character varying(255) DEFAULT ''::character varying NOT NULL,
    "Unpunctuality" boolean NOT NULL,
    CONSTRAINT "FixedIncome_DepositDayMonth_check" CHECK (((("DepositMonth" = ANY (ARRAY[1, 3, 5, 7, 8, 10, 12])) AND (("DepositDay" >= 1) AND ("DepositDay" <= 31))) OR (("DepositMonth" = ANY (ARRAY[4, 6, 9, 11])) AND (("DepositDay" >= 1) AND ("DepositDay" <= 30))) OR (("DepositMonth" = 2) AND (("DepositDay" >= 1) AND ("DepositDay" <= 29))))),
    CONSTRAINT "FixedIncome_MainClass_check" CHECK ((("MainClass")::text = ANY (ARRAY[('RegularIncome'::character varying)::text, ('IrregularIncome'::character varying)::text]))),
    CONSTRAINT "FixedIncome_SubClass_check" CHECK ((("SubClass")::text = ANY (ARRAY[('LaborIncome'::character varying)::text, ('BusinessIncome'::character varying)::text, ('PensionIncome'::character varying)::text, ('FinancialIncome'::character varying)::text, ('RentalIncome'::character varying)::text, ('OtherIncome'::character varying)::text])))
);


ALTER TABLE public."FixedIncome" OWNER TO postgres;

--
-- Name: TABLE "FixedIncome"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON TABLE public."FixedIncome" IS 'FixedIncome';


--
-- Name: COLUMN "FixedIncome"."Id"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."FixedIncome"."Id" IS 'PK';


--
-- Name: COLUMN "FixedIncome"."AccountEmail"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."FixedIncome"."AccountEmail" IS 'Account Email (ID)';


--
-- Name: COLUMN "FixedIncome"."MainClass"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."FixedIncome"."MainClass" IS 'MainClass';


--
-- Name: COLUMN "FixedIncome"."SubClass"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."FixedIncome"."SubClass" IS 'SubClass';


--
-- Name: COLUMN "FixedIncome"."Content"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."FixedIncome"."Content" IS 'Content';


--
-- Name: COLUMN "FixedIncome"."Amount"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."FixedIncome"."Amount" IS 'Amount';


--
-- Name: COLUMN "FixedIncome"."DepositMyAssetProductName"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."FixedIncome"."DepositMyAssetProductName" IS 'DepositMyAssetProductName';


--
-- Name: COLUMN "FixedIncome"."DepositMonth"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."FixedIncome"."DepositMonth" IS 'DepositMonth';


--
-- Name: COLUMN "FixedIncome"."DepositDay"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."FixedIncome"."DepositDay" IS 'DepositDay';


--
-- Name: COLUMN "FixedIncome"."MaturityDate"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."FixedIncome"."MaturityDate" IS 'MaturityDate';


--
-- Name: COLUMN "FixedIncome"."Created"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."FixedIncome"."Created" IS 'Created';


--
-- Name: COLUMN "FixedIncome"."Updated"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."FixedIncome"."Updated" IS 'Updated';


--
-- Name: COLUMN "FixedIncome"."Note"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."FixedIncome"."Note" IS 'Note';


--
-- Name: COLUMN "FixedIncome"."Unpunctuality"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."FixedIncome"."Unpunctuality" IS 'Unpunctuality';


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
    "Amount" numeric(20,4) NOT NULL,
    "DepositMyAssetProductName" character varying(255) NOT NULL,
    "Created" timestamp without time zone DEFAULT now() NOT NULL,
    "Updated" timestamp without time zone DEFAULT now() NOT NULL,
    "Note" character varying(255) DEFAULT ''::character varying NOT NULL,
    CONSTRAINT "Income_Content_check" CHECK ((("Content")::text ~ '\S'::text)),
    CONSTRAINT "Income_MainClass_check" CHECK ((("MainClass")::text = ANY (ARRAY[('RegularIncome'::character varying)::text, ('IrregularIncome'::character varying)::text]))),
    CONSTRAINT "Income_SubClass_check" CHECK ((("SubClass")::text = ANY (ARRAY[('LaborIncome'::character varying)::text, ('BusinessIncome'::character varying)::text, ('PensionIncome'::character varying)::text, ('FinancialIncome'::character varying)::text, ('RentalIncome'::character varying)::text, ('OtherIncome'::character varying)::text])))
);


ALTER TABLE public."Income" OWNER TO postgres;

--
-- Name: TABLE "Income"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON TABLE public."Income" IS 'Income';


--
-- Name: COLUMN "Income"."Id"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Income"."Id" IS 'PK';


--
-- Name: COLUMN "Income"."AccountEmail"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Income"."AccountEmail" IS 'Account Email (ID)';


--
-- Name: COLUMN "Income"."MainClass"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Income"."MainClass" IS 'MainClass';


--
-- Name: COLUMN "Income"."SubClass"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Income"."SubClass" IS 'SubClass';


--
-- Name: COLUMN "Income"."Content"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Income"."Content" IS 'Content';


--
-- Name: COLUMN "Income"."Amount"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Income"."Amount" IS 'Amount';


--
-- Name: COLUMN "Income"."DepositMyAssetProductName"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Income"."DepositMyAssetProductName" IS 'DepositMyAssetProductName';


--
-- Name: COLUMN "Income"."Created"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Income"."Created" IS 'Created';


--
-- Name: COLUMN "Income"."Updated"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Income"."Updated" IS 'Updated';


--
-- Name: COLUMN "Income"."Note"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."Income"."Note" IS 'Note';


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

COMMENT ON TABLE public."OtherCalendar" IS 'OtherCalendar';


--
-- Name: COLUMN "OtherCalendar"."AccountEmail"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."OtherCalendar"."AccountEmail" IS 'Account Email (ID)';


--
-- Name: COLUMN "OtherCalendar"."CalendarId"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."OtherCalendar"."CalendarId" IS 'Parent Calendar Id';


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
    CONSTRAINT "SubCategory_Order_check" CHECK (("Order" >= 0)),
    CONSTRAINT "SubCategory_Role_check" CHECK (((("Role")::text = 'Admin'::text) OR (("Role")::text = 'User'::text) OR (("Role")::text = 'Anonymous'::text)))
);


ALTER TABLE public."SubCategory" OWNER TO postgres;

--
-- Name: TABLE "SubCategory"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON TABLE public."SubCategory" IS 'SubCategory';


--
-- Name: COLUMN "SubCategory"."Id"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."SubCategory"."Id" IS 'ID';


--
-- Name: COLUMN "SubCategory"."CategoryId"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."SubCategory"."CategoryId" IS 'Parent Category ID';


--
-- Name: COLUMN "SubCategory"."Name"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."SubCategory"."Name" IS 'Name';


--
-- Name: COLUMN "SubCategory"."DisplayName"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."SubCategory"."DisplayName" IS 'DisplayName';


--
-- Name: COLUMN "SubCategory"."IconPath"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."SubCategory"."IconPath" IS 'IconPath';


--
-- Name: COLUMN "SubCategory"."Action"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."SubCategory"."Action" IS 'Action';


--
-- Name: COLUMN "SubCategory"."Role"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."SubCategory"."Role" IS 'Role';


--
-- Name: COLUMN "SubCategory"."Order"; Type: COMMENT; Schema: public; Owner: postgres
--

COMMENT ON COLUMN public."SubCategory"."Order" IS 'Order';


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

COPY public."Account" ("Email", "HashedPassword", "Nickname", "AvatarImagePath", "Role", "TimeZoneIanaId", "DefaultMonetaryUnit", "Locked", "LoginAttempt", "EmailConfirmed", "AgreedServiceTerms", "RegistrationToken", "ResetPasswordToken", "Created", "Updated", "Message", "Deleted", "SecurityStamp", "MustChangePassword") FROM stdin;
demo@maroik.com	$2a$13$e06KyPwRiFlJSgUeYoCv6uxedAHR1COyg9HsemA40xQbDVRQp/xfa	Demo	/upload/Management/Profile/Avatar/56EEF375-6DE8-49C2-BD23-673F2513F6D5.jpeg	User	Asia/Seoul	KRW	f	0	t	t	\N	\N	2021-07-12 22:46:59	2025-08-24 13:32:49.471924	This account is locked	f	5f69576a-557e-4d91-b810-9ba8930ae02a	f
admin@maroik.com	$2a$13$e06KyPwRiFlJSgUeYoCv6uxedAHR1COyg9HsemA40xQbDVRQp/xfa	Admin	/upload/Management/Profile/Avatar/DB0DDBD8-4AF9-4219-B034-F19773209AD0.png	Admin	Asia/Seoul	\N	f	0	t	t	\N	\N	2021-07-12 22:37:36	2026-04-29 23:11:46.589216	Success	f	31c7d654-a52d-4573-a4b1-84fd30c3ebbb	f
\.


--
-- Data for Name: Asset; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public."Asset" ("ProductName", "AccountEmail", "Item", "Amount", "MonetaryUnit", "Created", "Updated", "Note", "Deleted") FROM stdin;
USA Bank	demo@maroik.com	FreeDepositAndWithdrawal	-20.00	USD	2025-05-31 09:55:56.352656	2025-05-31 10:04:38.973066		f
USA Wallet	demo@maroik.com	FreeDepositAndWithdrawal	995.00	GBP	2025-05-31 09:54:32.019964	2025-07-01 13:08:59.264961	cvxcvxcvxcvxc	f
4bhk villa	demo@maroik.com	RealEstate	5000000.00	50000	2025-10-04 05:12:35.876248	2025-10-04 05:12:35.876316		f
school	demo@maroik.com	SavingsAsset	1000.00	12	2025-11-12 15:33:42.70333	2025-11-12 15:33:42.703358		f
Korea Wallet	demo@maroik.com	FreeDepositAndWithdrawal	2100.00	KRW	2025-05-31 09:55:14.159065	2026-03-10 05:34:47.935126		f
Korea Bank	demo@maroik.com	FreeDepositAndWithdrawal	555000.00	KRW	2025-05-31 09:56:05.871871	2026-03-10 05:35:15.814496		f
\.


--
-- Data for Name: Board; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public."Board" ("Id", "Type", "Title", "Content", "Writer", "Created", "Updated", "View", "Deleted", "Locked", "Noticed") FROM stdin;
250	FreeForum	' OR 1=1	<p>TEST</p>	Demo	2026-03-06 01:32:02.503171	2026-03-06 01:32:02.50321	1	t	f	f
208	PrivateNote	<img src="/e" onerror="console.log(1)">	<p>test</p>	Demo	2025-06-05 18:04:55.008513	2025-06-05 18:04:55.008539	25	f	f	f
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
\.


--
-- Data for Name: CalendarEvent; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public."CalendarEvent" ("Id", "CalendarId", "Title", "Description", "AllDay", "StartDate", "EndDate", "StartDateTimeZoneIanaId", "EndDateTimeZoneIanaId", "Location", "Status", "RecurrenceId", "Created", "Updated") FROM stdin;
797	6	111111		t	2025-06-10 00:00:00	2025-06-10 00:00:00	\N	\N	111111111111	Busy	\N	2025-06-18 04:10:02.175068	2025-06-18 04:10:02.175068
735	6	Sample		f	2025-05-31 23:00:00	2025-06-01 08:00:00	Asia/Seoul	Asia/Seoul	Location	Busy	\N	2025-05-31 09:52:07.450883	2025-05-31 09:52:07.45092
736	6	Long sample		t	2025-06-01 00:00:00	2025-06-07 00:00:00	\N	\N	Location	Busy	\N	2025-05-31 09:52:45.834435	2025-05-31 09:52:45.834435
746	6	Hello From Panamá! Great project		f	2025-06-04 14:00:00	2025-06-05 03:00:00	America/Detroit	Asia/Seoul	PTY	Busy	\N	2025-06-02 14:03:02.106273	2025-06-02 14:03:02.106274
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
1	Dashboard	Dashboard	nav-icon fas fa-tachometer-alt	Dashboard	AdminIndex	Admin	0
3	Dashboard	Dashboard	nav-icon fas fa-tachometer-alt	Dashboard	UserIndex	User	0
12	Dashboard	Dashboard	nav-icon fas fa-tachometer-alt	Dashboard	AnonymousIndex	Anonymous	0
9	Notice	Notice	nav-icon fas fa-bell	Notice		User	4
4	Management	Management	nav-icon fas fa-cog	Management		User	6
19	Calendar	Calendar	nav-icon far fa-calendar-alt	Calendar	AnonymousIndex	Anonymous	2
18	Forum	Forum	nav-icon fas fa-comments	Forum		Anonymous	3
20	Calendar	Calendar	nav-icon far fa-calendar-alt	Calendar	UserIndex	User	2
17	Forum	Forum	nav-icon fas fa-comments	Forum		User	3
21	Calendar	Calendar	nav-icon far fa-calendar-alt	Calendar	AdminIndex	Admin	2
16	Forum	Forum	nav-icon fas fa-comments	Forum		Admin	3
2	Management	Management	nav-icon fas fa-cog	Management		Admin	4
8	AccountBook	AccountBook	nav-icon fa fa-wallet	AccountBook		User	5
\.


--
-- Data for Name: Expenditure; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public."Expenditure" ("Id", "AccountEmail", "MainClass", "SubClass", "Content", "Amount", "PaymentMethod", "MyDepositAsset", "Created", "Updated", "Note") FROM stdin;
5148	demo@maroik.com	RegularSavings	Deposit	Deposit	10.00	USA Bank	USA Wallet	2025-06-03 10:00:52	2025-05-31 10:04:38.965386	
5146	demo@maroik.com	RegularSavings	Deposit	Deposit	100.00	Korea Bank	Korea Wallet	2025-06-25 10:00:52	2026-03-10 05:34:47.926672	
5149	demo@maroik.com	ConsumerSpending	MealOrEatOutExpenses	Food	20.00	USA Bank	\N	2025-06-11 10:00:52	2025-05-31 10:04:31.424056	
5147	demo@maroik.com	ConsumerSpending	MealOrEatOutExpenses	Food	200.00	Korea Bank	\N	2025-06-20 10:00:52	2025-05-31 10:04:45.612039	
5265	demo@maroik.com	ConsumerSpending	MealOrEatOutExpenses	rewr	45000.00	Korea Bank	\N	2025-06-25 02:10:06	2026-03-10 05:35:15.809917	
\.


--
-- Data for Name: FixedExpenditure; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public."FixedExpenditure" ("Id", "AccountEmail", "MainClass", "SubClass", "Content", "Amount", "PaymentMethod", "MyDepositAsset", "DepositMonth", "DepositDay", "MaturityDate", "Created", "Updated", "Note", "Unpunctuality") FROM stdin;
318	demo@maroik.com	ConsumerSpending	MealOrEatOutExpenses	Food	10.00	USA Wallet	\N	1	1	9999-12-31 00:00:00	2025-05-31 09:58:36.018467	2025-05-31 09:58:36.018467		f
317	demo@maroik.com	ConsumerSpending	MealOrEatOutExpenses	Food	1000.00	Korea Wallet	\N	1	1	9999-12-31 00:00:00	2025-05-31 09:58:29.099381	2025-05-31 09:58:40.076744		t
\.


--
-- Data for Name: FixedIncome; Type: TABLE DATA; Schema: public; Owner: postgres
--

COPY public."FixedIncome" ("Id", "AccountEmail", "MainClass", "SubClass", "Content", "Amount", "DepositMyAssetProductName", "DepositMonth", "DepositDay", "MaturityDate", "Created", "Updated", "Note", "Unpunctuality") FROM stdin;
102	demo@maroik.com	RegularIncome	LaborIncome	Wage	2000000.00	Korea Bank	1	1	9999-12-31 00:00:00	2025-05-31 09:56:33.634332	2025-05-31 09:58:11.768029		t
104	demo@maroik.com	RegularIncome	PensionIncome	Wage	3000.00	Korea Wallet	1	1	9999-12-31 00:00:00	2025-05-31 09:57:07.962648	2025-05-31 09:57:07.962648		f
105	demo@maroik.com	RegularIncome	FinancialIncome	Wage	3000.00	USA Wallet	1	1	9999-12-31 00:00:00	2025-05-31 09:57:22.008801	2025-05-31 09:57:22.008801		f
106	demo@maroik.com	RegularIncome	FinancialIncome	배당수입	100000.00	Korea Bank	9	1	2025-12-31 00:00:00	2025-06-05 02:32:11.446781	2025-06-05 02:32:11.446781		f
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

SELECT pg_catalog.setval('public."BoardAttachedFile_Id_seq"', 1, false);


--
-- Name: BoardComment_Id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public."BoardComment_Id_seq"', 1, false);


--
-- Name: Board_Id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public."Board_Id_seq"', 250, true);


--
-- Name: CalendarEventAttachedFile_Id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public."CalendarEventAttachedFile_Id_seq"', 1, false);


--
-- Name: CalendarEventReminder_Id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public."CalendarEventReminder_Id_seq"', 1, false);


--
-- Name: CalendarEvent_Id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public."CalendarEvent_Id_seq"', 797, true);


--
-- Name: CalendarRecurrence_Id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public."CalendarRecurrence_Id_seq"', 1, false);


--
-- Name: Calendar_Id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public."Calendar_Id_seq"', 6, true);


--
-- Name: Category_Id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public."Category_Id_seq"', 24, true);


--
-- Name: Expenditure_Id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public."Expenditure_Id_seq"', 5265, true);


--
-- Name: FixedExpenditure_Id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public."FixedExpenditure_Id_seq"', 318, true);


--
-- Name: FixedIncome_Id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public."FixedIncome_Id_seq"', 106, true);


--
-- Name: Income_Id_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public."Income_Id_seq"', 397, true);


--
-- Name: OtherCalendar_CalendarId_seq; Type: SEQUENCE SET; Schema: public; Owner: postgres
--

SELECT pg_catalog.setval('public."OtherCalendar_CalendarId_seq"', 1, false);


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
-- Name: Calendar Calendar_AccountEmail_Name_unique; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."Calendar"
    ADD CONSTRAINT "Calendar_AccountEmail_Name_unique" UNIQUE ("AccountEmail", "Name");


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
-- Name: Account_unique_index_0; Type: INDEX; Schema: public; Owner: postgres
--

-- Nickname uniqueness ignoring case: UNIQUE on lower("Nickname"). Account_Nickname_unique (above) is an exact-match
-- constraint, so on its own it would let "bob" register next to "Bob". The app checks this too
-- (NicknameExistsIgnoreCaseAsync); this index is what closes the race between two concurrent registrations.
-- Named like the other indexes ("<Table>_index_<n>"). EF Core / Npgsql cannot model an expression index, so it
-- exists only here (see ApplicationDbContext); Maroik.Core.Service's DbExceptionExtensions recognizes it by name.
CREATE UNIQUE INDEX "Account_unique_index_0" ON public."Account" USING btree (lower(("Nickname")::text));


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
-- Name: Board_index_0; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "Board_index_0" ON public."Board" USING btree ("Type", "Noticed", "Id");


--
-- Name: CalendarEventAttachedFile_index_0; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "CalendarEventAttachedFile_index_0" ON public."CalendarEventAttachedFile" USING btree ("CalendarEventId");


--
-- Name: CalendarEventReminder_index_0; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "CalendarEventReminder_index_0" ON public."CalendarEventReminder" USING btree ("CalendarEventId");


--
-- Name: CalendarEvent_index_1; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "CalendarEvent_index_1" ON public."CalendarEvent" USING btree ("RecurrenceId");


--
-- Name: CalendarEvent_index_2; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "CalendarEvent_index_2" ON public."CalendarEvent" USING btree ("CalendarId", "StartDate");


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
-- Name: Expenditure_index_1; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "Expenditure_index_1" ON public."Expenditure" USING btree ("AccountEmail", "Created");


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
-- Name: Income_index_1; Type: INDEX; Schema: public; Owner: postgres
--

CREATE INDEX "Income_index_1" ON public."Income" USING btree ("AccountEmail", "Created");


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
    ADD CONSTRAINT "Calendar_fk_0" FOREIGN KEY ("AccountEmail") REFERENCES public."Account"("Email") ON UPDATE CASCADE;


--
-- Name: Expenditure Expenditure_fk_0; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."Expenditure"
    ADD CONSTRAINT "Expenditure_fk_0" FOREIGN KEY ("PaymentMethod", "AccountEmail") REFERENCES public."Asset"("ProductName", "AccountEmail") ON UPDATE CASCADE;


--
-- Name: Expenditure Expenditure_fk_1; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."Expenditure"
    ADD CONSTRAINT "Expenditure_fk_1" FOREIGN KEY ("MyDepositAsset", "AccountEmail") REFERENCES public."Asset"("ProductName", "AccountEmail") ON UPDATE CASCADE;


--
-- Name: FixedExpenditure FixedExpenditure_fk_0; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."FixedExpenditure"
    ADD CONSTRAINT "FixedExpenditure_fk_0" FOREIGN KEY ("PaymentMethod", "AccountEmail") REFERENCES public."Asset"("ProductName", "AccountEmail") ON UPDATE CASCADE;


--
-- Name: FixedExpenditure FixedExpenditure_fk_1; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public."FixedExpenditure"
    ADD CONSTRAINT "FixedExpenditure_fk_1" FOREIGN KEY ("MyDepositAsset", "AccountEmail") REFERENCES public."Asset"("ProductName", "AccountEmail") ON UPDATE CASCADE;


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
    ADD CONSTRAINT "OtherCalendar_fk_0" FOREIGN KEY ("AccountEmail") REFERENCES public."Account"("Email") ON UPDATE CASCADE;


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

\unrestrict 1PZgDvTs1dJDSEjIBAFAWXcty99zpjjvh78z1tw18GGhJoYb92e6eLbOUdSeS6H

