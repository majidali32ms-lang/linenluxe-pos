-- ============================================================================
-- LinenLuxe POS — POS customers + POS orders
--
-- The POS keeps its OWN order list, completely separate from the website's
-- orders. Nothing here touches profiles / orders / order_items / services or
-- any existing policy — this file only ADDS new tables.
--
-- A POS order belongs to EITHER:
--   * a registered website customer  (profile_id  -> profiles)
--   * a walk-in customer             (pos_customer_id -> pos_customers)
-- The name and phone are also snapshotted onto the order, so an invoice always
-- prints exactly what was agreed at the counter.
--
-- Run the whole file once in the Supabase SQL Editor. Safe to re-run.
-- ============================================================================

-- ---------- Walk-in customers (no website account) ----------
create table if not exists public.pos_customers (
  id          uuid primary key default gen_random_uuid(),
  full_name   text not null,
  phone       text,
  notes       text,
  created_at  timestamptz not null default now(),
  updated_at  timestamptz not null default now()
);

-- Staff look walk-ins up by phone at the counter
create index if not exists pos_customers_phone_idx on public.pos_customers(phone);
create index if not exists pos_customers_name_idx  on public.pos_customers(lower(full_name));

drop trigger if exists pos_customers_set_updated_at on public.pos_customers;
create trigger pos_customers_set_updated_at before update on public.pos_customers
  for each row execute function public.set_updated_at();

-- ---------- POS orders ----------
-- Counter orders are numbered P00001, P00002 … so they can never be confused
-- with the website's LL00001 orders.
create sequence if not exists public.pos_order_number_seq;

create table if not exists public.pos_orders (
  id                uuid primary key default gen_random_uuid(),
  order_number      text unique not null
                      default ('P' || lpad(nextval('public.pos_order_number_seq')::text, 5, '0')),

  -- Customer: exactly one of these two is set
  profile_id        uuid references public.profiles(id)      on delete set null,  -- registered
  pos_customer_id   uuid references public.pos_customers(id) on delete set null,  -- walk-in

  -- Snapshot of who the order was for (survives later edits)
  customer_name     text not null,
  customer_phone    text,

  -- Reuses the website's status names so both lists speak the same language
  status            public.order_status not null default 'booked',

  -- Order options
  is_express        boolean not null default false,   -- Express service (+50%)
  payment_method    text    not null default 'cod',   -- 'cod' | 'online'
  is_paid           boolean not null default false,
  vat_enabled       boolean not null default false,   -- was 5% VAT applied
  notes             text,

  -- Money (AED) — every figure stored so the invoice can be reprinted exactly
  subtotal          numeric(10,2) not null default 0,
  express_surcharge numeric(10,2) not null default 0,
  discount          numeric(10,2) not null default 0,
  vat_amount        numeric(10,2) not null default 0,
  total             numeric(10,2) not null default 0,

  created_by        uuid references public.profiles(id) on delete set null,  -- which staff member
  created_at        timestamptz not null default now(),
  updated_at        timestamptz not null default now(),

  -- A POS order must have a customer of one kind or the other
  constraint pos_orders_has_customer
    check (profile_id is not null or pos_customer_id is not null),
  constraint pos_orders_payment_method
    check (payment_method in ('cod', 'online'))
);

create index if not exists pos_orders_status_idx   on public.pos_orders(status);
create index if not exists pos_orders_created_idx  on public.pos_orders(created_at desc);
create index if not exists pos_orders_phone_idx    on public.pos_orders(customer_phone);
create index if not exists pos_orders_profile_idx  on public.pos_orders(profile_id);

drop trigger if exists pos_orders_set_updated_at on public.pos_orders;
create trigger pos_orders_set_updated_at before update on public.pos_orders
  for each row execute function public.set_updated_at();

-- ---------- POS order lines ----------
create table if not exists public.pos_order_items (
  id           uuid primary key default gen_random_uuid(),
  order_id     uuid not null references public.pos_orders(id) on delete cascade,
  pos_item_id  uuid references public.pos_items(id) on delete set null,

  -- Snapshots, so a later price-list change never rewrites history
  item_name    text not null,
  category     text,
  service_type text not null,                       -- 'clean_press' | 'press_only'
  unit_price   numeric(10,2) not null,
  quantity     integer not null check (quantity > 0),
  line_total   numeric(10,2) not null,

  created_at   timestamptz not null default now(),

  constraint pos_order_items_service_type
    check (service_type in ('clean_press', 'press_only'))
);

create index if not exists pos_order_items_order_idx on public.pos_order_items(order_id);

-- ============================================================================
-- ROW LEVEL SECURITY — the POS is staff-only
-- ============================================================================
alter table public.pos_customers   enable row level security;
alter table public.pos_orders      enable row level security;
alter table public.pos_order_items enable row level security;

drop policy if exists pos_customers_staff on public.pos_customers;
create policy pos_customers_staff on public.pos_customers for all
  using (public.is_staff()) with check (public.is_staff());

drop policy if exists pos_orders_staff on public.pos_orders;
create policy pos_orders_staff on public.pos_orders for all
  using (public.is_staff()) with check (public.is_staff());

drop policy if exists pos_order_items_staff on public.pos_order_items;
create policy pos_order_items_staff on public.pos_order_items for all
  using (public.is_staff()) with check (public.is_staff());

notify pgrst, 'reload schema';

-- ============================================================================
-- DONE. Check with:
--   select count(*) from public.pos_customers;    -- 0
--   select count(*) from public.pos_orders;       -- 0
--   select count(*) from public.pos_order_items;  -- 0
-- ============================================================================
