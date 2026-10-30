-- ============================================================================
-- LinenLuxe POS — allow 'card' as a payment method
--
-- The shop takes COD, Online and Card; pos_orders was created in
-- 02_pos_orders.sql accepting only the first two.
--
-- This touches ONLY pos_orders, which belongs to the POS. The website's own
-- tables and its order_status enum are not involved, so nothing the web app
-- depends on changes.
--
-- Run once in the Supabase SQL Editor. Safe to re-run.
-- ============================================================================

alter table public.pos_orders
  drop constraint if exists pos_orders_payment_method;

alter table public.pos_orders
  add constraint pos_orders_payment_method
  check (payment_method in ('cod', 'online', 'card'));

notify pgrst, 'reload schema';

-- ============================================================================
-- DONE. Check with:
--   select conname, pg_get_constraintdef(oid)
--   from pg_constraint
--   where conrelid = 'public.pos_orders'::regclass
--     and conname = 'pos_orders_payment_method';
--
-- Expect: CHECK (payment_method = ANY (ARRAY['cod', 'online', 'card']))
-- ============================================================================
