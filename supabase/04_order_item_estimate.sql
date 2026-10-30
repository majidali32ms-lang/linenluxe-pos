-- ============================================================================
-- LinenLuxe POS — remember what an item was going to cost before inspection
--
-- The price list's own note says the final price follows inspection, and staff
-- can now change a line's rate at the counter. Until this column existed only
-- the charged rate was kept, so a reopened order — and its reprinted invoice —
-- could not say a price had ever moved.
--
-- est_unit_price holds the price-list rate the line started at. It is null when
-- nothing was changed, which is the common case; a value means "this was
-- repriced, and here is what from".
--
-- Mirrors the website's own est_unit_price on its order_items, so both invoices
-- can show the same "Estimated / Final" note.
--
-- Run once in the Supabase SQL Editor. Safe to re-run.
-- ============================================================================

alter table public.pos_order_items
  add column if not exists est_unit_price numeric(10,2);

comment on column public.pos_order_items.est_unit_price is
  'Price-list rate before inspection. Null when the line was never repriced.';

notify pgrst, 'reload schema';

-- ============================================================================
-- DONE. Check with:
--   select column_name, data_type, is_nullable
--   from information_schema.columns
--   where table_name = 'pos_order_items' and column_name = 'est_unit_price';
-- ============================================================================
