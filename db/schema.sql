-- OrgMind database structure (PostgreSQL 16 with the pgvector extension).
--
-- Structure only: no data, no users, no keys.
-- To create it on a fresh database:
--     CREATE DATABASE orgmind;
--     psql -U postgres -d orgmind -f schema.sql
-- The server needs pgvector installed (the Docker image "pgvector/pgvector:pg16" has it).
--
-- Times: columns without a time zone hold UTC. The .NET API marks all dates it sends as UTC.
-- Generated with pg_dump --schema-only; keep this file updated whenever a table changes.


-- Dumped from database version 16.15 (Debian 16.15-1.pgdg12+2)
-- Dumped by pg_dump version 16.15 (Debian 16.15-1.pgdg12+2)

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

--
-- Name: vector; Type: EXTENSION; Schema: -; Owner: -
--

CREATE EXTENSION IF NOT EXISTS vector WITH SCHEMA public;


--
-- Name: EXTENSION vector; Type: COMMENT; Schema: -; Owner: -
--

COMMENT ON EXTENSION vector IS 'vector data type and ivfflat and hnsw access methods';


SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: audit_logs; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.audit_logs (
    id integer NOT NULL,
    event_type character varying(50) NOT NULL,
    user_id character varying(320),
    org_id character varying(100),
    details jsonb,
    severity character varying(20) DEFAULT 'INFO'::character varying,
    "timestamp" timestamp without time zone DEFAULT CURRENT_TIMESTAMP
);


--
-- Name: audit_logs_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.audit_logs_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: audit_logs_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.audit_logs_id_seq OWNED BY public.audit_logs.id;


--
-- Name: conversation_memory; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.conversation_memory (
    id integer NOT NULL,
    user_id character varying(100) NOT NULL,
    org_id character varying(100) NOT NULL,
    role character varying(20) NOT NULL,
    content text NOT NULL,
    "timestamp" timestamp without time zone DEFAULT CURRENT_TIMESTAMP
);


--
-- Name: conversation_memory_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.conversation_memory_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: conversation_memory_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.conversation_memory_id_seq OWNED BY public.conversation_memory.id;


--
-- Name: document_chunks; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.document_chunks (
    id integer NOT NULL,
    org_id character varying(100) NOT NULL,
    doc_name character varying(255) NOT NULL,
    chunk_index integer NOT NULL,
    content text NOT NULL,
    embedding public.vector(384)
);


--
-- Name: document_chunks_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.document_chunks_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: document_chunks_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.document_chunks_id_seq OWNED BY public.document_chunks.id;


--
-- Name: documents; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.documents (
    org_id character varying(100) NOT NULL,
    doc_name character varying(255) NOT NULL,
    visibility character varying(20) DEFAULT 'internal'::character varying NOT NULL,
    department character varying(100),
    uploaded_by character varying(255),
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT documents_check CHECK ((((visibility)::text <> 'department'::text) OR (department IS NOT NULL))),
    CONSTRAINT documents_visibility_check CHECK (((visibility)::text = ANY ((ARRAY['public'::character varying, 'registered'::character varying, 'internal'::character varying, 'department'::character varying, 'admin_only'::character varying])::text[])))
);


--
-- Name: form_submissions; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.form_submissions (
    id integer NOT NULL,
    org_id text NOT NULL,
    template_id integer NOT NULL,
    template_name text NOT NULL,
    submitted_by text,
    department text,
    data jsonb DEFAULT '{}'::jsonb NOT NULL,
    status text DEFAULT 'new'::text NOT NULL,
    handled_by text,
    created_at timestamp without time zone DEFAULT (now() AT TIME ZONE 'utc'::text) NOT NULL,
    updated_at timestamp without time zone DEFAULT (now() AT TIME ZONE 'utc'::text) NOT NULL,
    status_note text,
    CONSTRAINT form_submissions_data_check CHECK ((jsonb_typeof(data) = 'object'::text)),
    CONSTRAINT form_submissions_note_len CHECK (((status_note IS NULL) OR (char_length(status_note) <= 300))),
    CONSTRAINT form_submissions_status_check CHECK ((status = ANY (ARRAY['new'::text, 'in_progress'::text, 'approved'::text, 'rejected'::text, 'done'::text])))
);


--
-- Name: form_submissions_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.form_submissions_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: form_submissions_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.form_submissions_id_seq OWNED BY public.form_submissions.id;


--
-- Name: form_templates; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.form_templates (
    id integer NOT NULL,
    org_id text NOT NULL,
    name text NOT NULL,
    description text,
    audience text DEFAULT 'customer'::text NOT NULL,
    department text,
    fields jsonb DEFAULT '[]'::jsonb NOT NULL,
    active boolean DEFAULT true NOT NULL,
    created_by text NOT NULL,
    created_at timestamp without time zone DEFAULT (now() AT TIME ZONE 'utc'::text) NOT NULL,
    updated_at timestamp without time zone DEFAULT (now() AT TIME ZONE 'utc'::text) NOT NULL,
    CONSTRAINT form_templates_audience_check CHECK ((audience = ANY (ARRAY['customer'::text, 'employee'::text, 'public'::text]))),
    CONSTRAINT form_templates_fields_check CHECK ((jsonb_typeof(fields) = 'array'::text))
);


--
-- Name: form_templates_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.form_templates_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: form_templates_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.form_templates_id_seq OWNED BY public.form_templates.id;


--
-- Name: invites; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.invites (
    code text NOT NULL,
    org_id text NOT NULL,
    role text DEFAULT 'member'::text NOT NULL,
    expires_at timestamp with time zone NOT NULL,
    used_at timestamp with time zone,
    created_by text NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    department text
);


--
-- Name: organizations; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.organizations (
    id text NOT NULL,
    name text NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    allow_customer_signup boolean DEFAULT true NOT NULL
);


--
-- Name: staff_messages; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.staff_messages (
    id integer NOT NULL,
    org_id text NOT NULL,
    channel text NOT NULL,
    author text NOT NULL,
    body text NOT NULL,
    created_at timestamp without time zone DEFAULT (now() AT TIME ZONE 'UTC'::text) NOT NULL,
    CONSTRAINT staff_messages_body_check CHECK (((char_length(body) >= 1) AND (char_length(body) <= 1000)))
);


--
-- Name: staff_messages_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.staff_messages_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: staff_messages_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.staff_messages_id_seq OWNED BY public.staff_messages.id;


--
-- Name: staff_reads; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.staff_reads (
    org_id text NOT NULL,
    user_email text NOT NULL,
    channel text NOT NULL,
    last_read_id integer DEFAULT 0 NOT NULL
);


--
-- Name: thread_messages; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.thread_messages (
    id integer NOT NULL,
    org_id text NOT NULL,
    kind text NOT NULL,
    ref_id integer NOT NULL,
    author text NOT NULL,
    body text NOT NULL,
    read_by_owner boolean DEFAULT false NOT NULL,
    created_at timestamp without time zone DEFAULT (now() AT TIME ZONE 'UTC'::text) NOT NULL,
    CONSTRAINT thread_messages_body_check CHECK (((char_length(body) >= 1) AND (char_length(body) <= 1000))),
    CONSTRAINT thread_messages_kind_check CHECK ((kind = ANY (ARRAY['ticket'::text, 'submission'::text])))
);


--
-- Name: thread_messages_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.thread_messages_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: thread_messages_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.thread_messages_id_seq OWNED BY public.thread_messages.id;


--
-- Name: tickets; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.tickets (
    id integer NOT NULL,
    org_id text NOT NULL,
    created_by text NOT NULL,
    issue text NOT NULL,
    priority text DEFAULT 'medium'::text NOT NULL,
    status text DEFAULT 'open'::text NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    updated_at timestamp with time zone DEFAULT now() NOT NULL,
    department text,
    sentiment text,
    escalated boolean DEFAULT false NOT NULL,
    CONSTRAINT tickets_priority_check CHECK ((priority = ANY (ARRAY['low'::text, 'medium'::text, 'high'::text]))),
    CONSTRAINT tickets_sentiment_check CHECK ((sentiment = ANY (ARRAY['positive'::text, 'neutral'::text, 'negative'::text]))),
    CONSTRAINT tickets_status_check CHECK ((status = ANY (ARRAY['open'::text, 'in_progress'::text, 'resolved'::text])))
);


--
-- Name: tickets_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.tickets_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: tickets_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.tickets_id_seq OWNED BY public.tickets.id;


--
-- Name: user_profiles; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.user_profiles (
    id integer NOT NULL,
    user_id character varying(100) NOT NULL,
    org_id character varying(100) NOT NULL,
    key character varying(100) NOT NULL,
    value text NOT NULL,
    updated_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP
);


--
-- Name: user_profiles_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.user_profiles_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: user_profiles_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.user_profiles_id_seq OWNED BY public.user_profiles.id;


--
-- Name: users; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.users (
    id integer NOT NULL,
    email character varying(255) NOT NULL,
    password_hash character varying(255) NOT NULL,
    org_id character varying(100) NOT NULL,
    role character varying(50) DEFAULT 'member'::character varying,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    department character varying(100),
    employee_id text
);


--
-- Name: users_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.users_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: users_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.users_id_seq OWNED BY public.users.id;


--
-- Name: audit_logs id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.audit_logs ALTER COLUMN id SET DEFAULT nextval('public.audit_logs_id_seq'::regclass);


--
-- Name: conversation_memory id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.conversation_memory ALTER COLUMN id SET DEFAULT nextval('public.conversation_memory_id_seq'::regclass);


--
-- Name: document_chunks id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.document_chunks ALTER COLUMN id SET DEFAULT nextval('public.document_chunks_id_seq'::regclass);


--
-- Name: form_submissions id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.form_submissions ALTER COLUMN id SET DEFAULT nextval('public.form_submissions_id_seq'::regclass);


--
-- Name: form_templates id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.form_templates ALTER COLUMN id SET DEFAULT nextval('public.form_templates_id_seq'::regclass);


--
-- Name: staff_messages id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.staff_messages ALTER COLUMN id SET DEFAULT nextval('public.staff_messages_id_seq'::regclass);


--
-- Name: thread_messages id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.thread_messages ALTER COLUMN id SET DEFAULT nextval('public.thread_messages_id_seq'::regclass);


--
-- Name: tickets id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.tickets ALTER COLUMN id SET DEFAULT nextval('public.tickets_id_seq'::regclass);


--
-- Name: user_profiles id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.user_profiles ALTER COLUMN id SET DEFAULT nextval('public.user_profiles_id_seq'::regclass);


--
-- Name: users id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.users ALTER COLUMN id SET DEFAULT nextval('public.users_id_seq'::regclass);


--
-- Name: audit_logs audit_logs_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.audit_logs
    ADD CONSTRAINT audit_logs_pkey PRIMARY KEY (id);


--
-- Name: conversation_memory conversation_memory_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.conversation_memory
    ADD CONSTRAINT conversation_memory_pkey PRIMARY KEY (id);


--
-- Name: document_chunks document_chunks_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.document_chunks
    ADD CONSTRAINT document_chunks_pkey PRIMARY KEY (id);


--
-- Name: documents documents_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documents
    ADD CONSTRAINT documents_pkey PRIMARY KEY (org_id, doc_name);


--
-- Name: form_submissions form_submissions_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.form_submissions
    ADD CONSTRAINT form_submissions_pkey PRIMARY KEY (id);


--
-- Name: form_templates form_templates_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.form_templates
    ADD CONSTRAINT form_templates_pkey PRIMARY KEY (id);


--
-- Name: invites invites_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.invites
    ADD CONSTRAINT invites_pkey PRIMARY KEY (code);


--
-- Name: organizations organizations_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.organizations
    ADD CONSTRAINT organizations_pkey PRIMARY KEY (id);


--
-- Name: staff_messages staff_messages_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.staff_messages
    ADD CONSTRAINT staff_messages_pkey PRIMARY KEY (id);


--
-- Name: staff_reads staff_reads_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.staff_reads
    ADD CONSTRAINT staff_reads_pkey PRIMARY KEY (org_id, user_email, channel);


--
-- Name: thread_messages thread_messages_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.thread_messages
    ADD CONSTRAINT thread_messages_pkey PRIMARY KEY (id);


--
-- Name: tickets tickets_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.tickets
    ADD CONSTRAINT tickets_pkey PRIMARY KEY (id);


--
-- Name: user_profiles user_profiles_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.user_profiles
    ADD CONSTRAINT user_profiles_pkey PRIMARY KEY (id);


--
-- Name: user_profiles user_profiles_user_id_org_id_key_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.user_profiles
    ADD CONSTRAINT user_profiles_user_id_org_id_key_key UNIQUE (user_id, org_id, key);


--
-- Name: users users_email_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.users
    ADD CONSTRAINT users_email_key UNIQUE (email);


--
-- Name: users users_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.users
    ADD CONSTRAINT users_pkey PRIMARY KEY (id);


--
-- Name: idx_audit_org_time; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_audit_org_time ON public.audit_logs USING btree (org_id, "timestamp" DESC);


--
-- Name: idx_form_submissions_dept; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_form_submissions_dept ON public.form_submissions USING btree (org_id, department, status);


--
-- Name: idx_form_submissions_user; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_form_submissions_user ON public.form_submissions USING btree (org_id, submitted_by);


--
-- Name: idx_form_templates_org; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_form_templates_org ON public.form_templates USING btree (org_id, active);


--
-- Name: idx_users_employee_id; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX idx_users_employee_id ON public.users USING btree (org_id, employee_id) WHERE (employee_id IS NOT NULL);


--
-- Name: staff_messages_channel; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX staff_messages_channel ON public.staff_messages USING btree (org_id, channel, id);


--
-- Name: thread_messages_ref; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX thread_messages_ref ON public.thread_messages USING btree (org_id, kind, ref_id, created_at);


--
-- Name: tickets_dept_idx; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX tickets_dept_idx ON public.tickets USING btree (org_id, department);


--
-- Name: tickets_org_idx; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX tickets_org_idx ON public.tickets USING btree (org_id, created_at DESC);


--
-- Name: tickets_user_idx; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX tickets_user_idx ON public.tickets USING btree (org_id, created_by);


--
-- Name: users_email_lower_key; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX users_email_lower_key ON public.users USING btree (lower((email)::text));


--
-- Name: form_submissions form_submissions_template_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.form_submissions
    ADD CONSTRAINT form_submissions_template_id_fkey FOREIGN KEY (template_id) REFERENCES public.form_templates(id);


--
-- Name: invites invites_org_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.invites
    ADD CONSTRAINT invites_org_id_fkey FOREIGN KEY (org_id) REFERENCES public.organizations(id);


--
-- PostgreSQL database dump complete
--


-- Thumbs up/down on assistant answers. A thumbs-up keeps no text; a thumbs-down keeps the
-- question, part of the answer and an optional reason, readable by admins only.
CREATE TABLE public.answer_feedback (
    id serial PRIMARY KEY,
    org_id text NOT NULL,
    user_email text NOT NULL,
    rating text NOT NULL CHECK (rating IN ('up', 'down')),
    question text CHECK (question IS NULL OR char_length(question) <= 500),
    answer text CHECK (answer IS NULL OR char_length(answer) <= 1500),
    note text CHECK (note IS NULL OR char_length(note) <= 300),
    status text NOT NULL DEFAULT 'new' CHECK (status IN ('new', 'handled')),
    created_at timestamp without time zone NOT NULL DEFAULT (now() AT TIME ZONE 'utc')
);

CREATE INDEX answer_feedback_org ON public.answer_feedback USING btree (org_id, rating, status, created_at DESC);

ALTER TABLE public.users
    ADD COLUMN IF NOT EXISTS display_name varchar(60),
    ADD COLUMN IF NOT EXISTS job_title varchar(60),
    ADD COLUMN IF NOT EXISTS phone varchar(30),
    ADD COLUMN IF NOT EXISTS bio varchar(300);