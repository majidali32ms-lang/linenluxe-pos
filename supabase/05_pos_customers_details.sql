-- ============================================================================
-- LinenLuxe POS — what a customer record needs to hold
--
-- pos_customers started as a name and a phone, which is all a walk-in needs.
-- Two things it now has to carry:
--
--   * An address. Deliveries cannot be grouped into rounds without one, and the
--     web invoice prints an address the POS would otherwise leave blank. The area
--     is kept apart from the address so a round can be built from it.
--
--   * A business account. A company is not a walk-in: there is a name to ring,
--     credit rather than payment at the counter, and often the customer's own TRN,
--     because they need a tax invoice made out to them. Adding these now means the
--     table is not migrated a second time when agreed rates and month-end
--     invoicing are built on top of it.
--
-- Touches pos_customers only, which belongs to the POS. The website's profiles
-- and addresses tables are untouched.
--
-- Run once in the Supabase SQL Editor. Safe to re-run.
-- ============================================================================

-- ---------- Walk-in or business ----------
alter table public.pos_customers
  add column if not exists customer_type text not null default 'walk_in';

alter table public.pos_customers
  drop constraint if exists pos_customers_type;

alter table public.pos_customers
  add constraint pos_customers_type
  check (customer_type in ('walk_in', 'business'));

-- ---------- Contact ----------
alter table public.pos_customers
  add column if not exists email text;

-- Who to ask for when you ring a company. Empty for a walk-in.
alter table public.pos_customers
  add column if not exists contact_person text;

-- ---------- Where to collect from and deliver to (2.4) ----------
alter table public.pos_customers
  add column if not exists address text;

-- Kept apart from the address so deliveries can be grouped by round.
alter table public.pos_customers
  add column if not exists area text;

-- ---------- Business account details (2.5 builds on these) ----------
-- The customer's own tax number, needed when they require a tax invoice. Not
-- the shop's TRN — that lives in settings (4.5).
alter table public.pos_customers
  add column if not exists trn text;

-- Days to pay. 0 means due on collection, which is how a walk-in works.
alter table public.pos_customers
  add column if not exists credit_days integer not null default 0;

alter table public.pos_customers
  drop constraint if exists pos_customers_credit_days;

alter table public.pos_customers
  add constraint pos_customers_credit_days check (credit_days >= 0);

-- ---------- Indexes ----------
-- Staff filter the list by type constantly; the area index is for 3.3's rounds.
create index if not exists pos_customers_type_idx on public.pos_customers(customer_type);
create index if not exists pos_customers_area_idx on public.pos_customers(lower(area));

comment on column public.pos_customers.customer_type is
  'walk_in | business. Business accounts get agreed rates and month-end invoicing (2.5).';
comment on column public.pos_customers.trn is
  'The CUSTOMER''s tax number, for their tax invoice. The shop''s own TRN is a setting.';
comment on column public.pos_customers.credit_days is
  'Days to pay. 0 = due on collection, which is how a walk-in pays.';

notify pgrst, 'reload schema';

-- ============================================================================
-- DONE. Check with:
--   select column_name, data_type, column_default
--   from information_schema.columns
--   where table_name = 'pos_customers'
--   order by ordinal_position;
--
-- Expect the original id / full_name / phone / notes / created_at / updated_at
-- plus customer_type, email, contact_person, address, area, trn, credit_days.
-- ============================================================================
