-- ============================================================================
-- LinenLuxe POS — the full POS price list (pos_items)
--
-- Source: LinenLuxe-Laundry-Price-List.pdf (163 items, 3 parts, 8 categories).
-- Each item has a "Clean & Press" rate and, where offered, a "Press Only" rate.
-- A dash (—) in the PDF means that service isn't offered -> stored as NULL.
--
-- SAFE FOR THE WEB APP: this only ADDS a new table. It does not touch
-- profiles / orders / order_items / services or any existing policy.
--
-- Run the whole file once in the Supabase SQL Editor. Safe to re-run
-- (it upserts, so prices update instead of duplicating).
-- ============================================================================

-- ---------- Table ----------
create table if not exists public.pos_items (
  id                 uuid primary key default gen_random_uuid(),
  part               text not null,               -- 'Apparel' | 'Home' | 'Special'
  category           text not null,               -- e.g. 'Men''s Clothing'
  name               text not null,               -- e.g. 'Kandura / Dishdasha'
  clean_press_price  numeric(10,2),               -- AED, null = not offered
  press_only_price   numeric(10,2),               -- AED, null = not offered
  sort_order         int  not null default 0,     -- keeps the printed price-list order
  is_active          boolean not null default true,
  created_at         timestamptz not null default now(),
  updated_at         timestamptz not null default now(),
  constraint pos_items_unique_name unique (category, name)
);

create index if not exists pos_items_active_sort_idx on public.pos_items(is_active, sort_order);
create index if not exists pos_items_category_idx    on public.pos_items(category);

-- Keep updated_at fresh (reuses the helper the web app already created)
drop trigger if exists pos_items_set_updated_at on public.pos_items;
create trigger pos_items_set_updated_at before update on public.pos_items
  for each row execute function public.set_updated_at();

-- ---------- Row level security ----------
alter table public.pos_items enable row level security;

-- Any signed-in user can read the catalog
drop policy if exists pos_items_select on public.pos_items;
create policy pos_items_select on public.pos_items for select
  using (auth.uid() is not null);

-- Only staff/admin can add, edit or remove items
drop policy if exists pos_items_write on public.pos_items;
create policy pos_items_write on public.pos_items for all
  using (public.is_staff()) with check (public.is_staff());

-- ============================================================================
-- SEED — 163 items in printed price-list order
-- ============================================================================
insert into public.pos_items (part, category, name, clean_press_price, press_only_price, sort_order) values

-- ---------- PART 1 · Apparel — Men's Clothing (26) ----------
('Apparel', 'Men''s Clothing', 'Under Wear',                    5,    2,    1),
('Apparel', 'Men''s Clothing', 'Tie',                           7,    5,    2),
('Apparel', 'Men''s Clothing', 'Inner Shirt',                   5,    3,    3),
('Apparel', 'Men''s Clothing', 'Boxer',                         5,    3,    4),
('Apparel', 'Men''s Clothing', 'Pyjama',                        10,   6,    5),
('Apparel', 'Men''s Clothing', 'Pull Over',                     15,   10,   6),
('Apparel', 'Men''s Clothing', 'Long Top',                      18,   12,   7),
('Apparel', 'Men''s Clothing', 'Sweatshirt',                    25,   12,   8),
('Apparel', 'Men''s Clothing', 'Sweater',                       15,   12,   9),
('Apparel', 'Men''s Clothing', 'T Shirt',                       14,   7,    10),
('Apparel', 'Men''s Clothing', 'Track Suit (2 Pcs)',            25,   15,   11),
('Apparel', 'Men''s Clothing', 'Trouser',                       16,   9,    12),
('Apparel', 'Men''s Clothing', 'Trouser - Security Uniform',    4,    2,    13),
('Apparel', 'Men''s Clothing', 'Suit (3 Pcs)',                  50,   35,   14),
('Apparel', 'Men''s Clothing', 'Suit (2 Pcs)',                  45,   30,   15),
('Apparel', 'Men''s Clothing', 'Waist Coat',                    15,   10,   16),
('Apparel', 'Men''s Clothing', 'Jacket',                        30,   15,   17),
('Apparel', 'Men''s Clothing', 'Jacket - Security Uniform',     6,    4,    18),
('Apparel', 'Men''s Clothing', 'Shirt',                         14,   8,    19),
('Apparel', 'Men''s Clothing', 'Shirt - Security Uniform',      3,    2,    20),
('Apparel', 'Men''s Clothing', 'Wazir',                         8,    5,    21),
('Apparel', 'Men''s Clothing', 'Kurtha',                        12,   7,    22),
('Apparel', 'Men''s Clothing', 'Khamis',                        10,   7,    23),
('Apparel', 'Men''s Clothing', 'Kandura / Dishdasha',           15,   10,   24),
('Apparel', 'Men''s Clothing', 'Gatra',                         6,    4,    25),
('Apparel', 'Men''s Clothing', 'Gafia (Arabic Cap)',            3,    1.5,  26),

-- ---------- PART 1 · Apparel — Women's Clothing (29) ----------
('Apparel', 'Women''s Clothing', 'Shorts',                      10,   6,    27),
('Apparel', 'Women''s Clothing', 'Leg Warmer',                  15,   8,    28),
('Apparel', 'Women''s Clothing', 'Cap',                         15,   8,    29),
('Apparel', 'Women''s Clothing', 'Bra',                         5,    3,    30),
('Apparel', 'Women''s Clothing', 'Apron',                       8,    5,    31),
('Apparel', 'Women''s Clothing', 'Top',                         15,   9,    32),
('Apparel', 'Women''s Clothing', 'Skirting',                    5,    3,    33),
('Apparel', 'Women''s Clothing', 'Skirt',                       14,   9,    34),
('Apparel', 'Women''s Clothing', 'Skirt Pleated',               20,   12,   35),
('Apparel', 'Women''s Clothing', 'Skirt Long',                  18,   12,   36),
('Apparel', 'Women''s Clothing', 'Small Vest',                  15,   8,    37),
('Apparel', 'Women''s Clothing', 'Shawl',                       15,   8,    38),
('Apparel', 'Women''s Clothing', 'Shalwar',                     9,    6,    39),
('Apparel', 'Women''s Clothing', 'Scarf / Sheila',              10,   6,    40),
('Apparel', 'Women''s Clothing', 'Saree',                       30,   15,   41),
('Apparel', 'Women''s Clothing', 'Saree Blouse',                10,   6,    42),
('Apparel', 'Women''s Clothing', 'Party Dress',                 60,   46,   43),
('Apparel', 'Women''s Clothing', 'Overcoat Long',               45,   25,   44),
('Apparel', 'Women''s Clothing', 'Over Coat',                   35,   24,   45),
('Apparel', 'Women''s Clothing', 'Night Dress',                 18,   11,   46),
('Apparel', 'Women''s Clothing', 'Long Vest',                   20,   12,   47),
('Apparel', 'Women''s Clothing', 'Ladies Dress',                30,   18,   48),
('Apparel', 'Women''s Clothing', 'Ladies Dress Long',           50,   38,   49),
('Apparel', 'Women''s Clothing', 'Dress Lining',                10,   7,    50),
('Apparel', 'Women''s Clothing', 'Coverall',                    30,   15,   51),
('Apparel', 'Women''s Clothing', 'Cardigan Small',              15,   10,   52),
('Apparel', 'Women''s Clothing', 'Blouse',                      13,   7,    53),
('Apparel', 'Women''s Clothing', 'Abaya Designer',              40,   20,   54),
('Apparel', 'Women''s Clothing', 'Abaya',                       20,   10,   55),

-- ---------- PART 1 · Apparel — Baby Items (19) ----------
('Apparel', 'Baby Items', 'Baby Waist Coat',                    15,   8,    56),
('Apparel', 'Baby Items', 'Baby Toy Small',                     3,    null, 57),
('Apparel', 'Baby Items', 'Baby Sweater',                       10,   5,    58),
('Apparel', 'Baby Items', 'Baby Skirt',                         7,    5,    59),
('Apparel', 'Baby Items', 'Baby Shirt',                         6,    4,    60),
('Apparel', 'Baby Items', 'Baby Pant',                          7,    5,    61),
('Apparel', 'Baby Items', 'Baby Overall',                       15,   10,   62),
('Apparel', 'Baby Items', 'Baby Kurtha',                        10,   7,    63),
('Apparel', 'Baby Items', 'Baby Kandura',                       10,   7,    64),
('Apparel', 'Baby Items', 'Baby Jacket',                        20,   15,   65),
('Apparel', 'Baby Items', 'Baby Head Protector',                5,    null, 66),
('Apparel', 'Baby Items', 'Baby Dress',                         15,   8,    67),
('Apparel', 'Baby Items', 'Baby Carrier',                       35,   null, 68),
('Apparel', 'Baby Items', 'Baby Carrier Accessory',             3,    null, 69),
('Apparel', 'Baby Items', 'Baby Car Seat',                      30,   null, 70),
('Apparel', 'Baby Items', 'Baby Cap',                           5,    3,    71),
('Apparel', 'Baby Items', 'Baby Blanket',                       15,   8,    72),
('Apparel', 'Baby Items', 'Baby Bib',                           15,   8,    73),
('Apparel', 'Baby Items', 'Baby Bedsheet',                      8,    4,    74),

-- ---------- PART 2 · Home — Bedding (22) ----------
('Home', 'Bedding', 'Sleeping Bag',                             80,   null, 75),
('Home', 'Bedding', 'Pillow Duckfeather',                       20,   null, 76),
('Home', 'Bedding', 'Pillow Cover',                             7,    5,    77),
('Home', 'Bedding', 'Pillow',                                   12,   null, 78),
('Home', 'Bedding', 'Mattress Protector',                       35,   null, 79),
('Home', 'Bedding', 'Duvet Cover',                              30,   20,   80),
('Home', 'Bedding', 'Duvet',                                    40,   null, 81),
('Home', 'Bedding', 'Comforter Single',                         30,   null, 82),
('Home', 'Bedding', 'Comforter Double',                         40,   20,   83),
('Home', 'Bedding', 'Blanket Wool',                             45,   null, 84),
('Home', 'Bedding', 'Blanket Single',                           24,   null, 85),
('Home', 'Bedding', 'Blanket Double',                           40,   null, 86),
('Home', 'Bedding', 'Big Pillow Cover',                         8,    4,    87),
('Home', 'Bedding', 'Bed Towel',                                12,   7,    88),
('Home', 'Bedding', 'Bedspread Single',                         25,   13,   89),
('Home', 'Bedding', 'Bedspread Duck Feather',                   40,   20,   90),
('Home', 'Bedding', 'Bedspread Double',                         35,   20,   91),
('Home', 'Bedding', 'Bed Sheet Single',                         22,   14,   92),
('Home', 'Bedding', 'Bed Sheet Double',                         25,   15,   93),
('Home', 'Bedding', 'Bed Cover Single',                         25,   15,   94),
('Home', 'Bedding', 'Bed Cover Double',                         30,   15,   95),
('Home', 'Bedding', 'Bed Canopy',                               85,   null, 96),

-- ---------- PART 2 · Home — Household Linens (29) ----------
('Home', 'Household Linens', 'Jacket Special Woolean',          80,   40,   97),
('Home', 'Household Linens', 'Handbag Leather',                 50,   null, 98),
('Home', 'Household Linens', 'Gown',                            60,   35,   99),
('Home', 'Household Linens', 'Air Bed',                         300,  null, 100),
('Home', 'Household Linens', 'Yoga Mat',                        40,   null, 101),
('Home', 'Household Linens', 'Throw',                           15,   10,   102),
('Home', 'Household Linens', 'Table Runner',                    29,   16,   103),
('Home', 'Household Linens', 'Table Mat',                       10,   6,    104),
('Home', 'Household Linens', 'Table Cloth',                     15,   12,   105),
('Home', 'Household Linens', 'Sofa Cover X-large',              75,   null, 106),
('Home', 'Household Linens', 'Sofa Cover Medium',               27,   15,   107),
('Home', 'Household Linens', 'Sofa Cover Big',                  35,   20,   108),
('Home', 'Household Linens', 'Round Table Cloth',               10,   null, 109),
('Home', 'Household Linens', 'Prayer Mat',                      35,   null, 110),
('Home', 'Household Linens', 'Pet Cushion',                     35,   null, 111),
('Home', 'Household Linens', 'Net',                             15,   null, 112),
('Home', 'Household Linens', 'Kitchen Towel',                   1.95, null, 113),
('Home', 'Household Linens', 'Floor Mat',                       15,   null, 114),
('Home', 'Household Linens', 'Dust Bag',                        8,    null, 115),
('Home', 'Household Linens', 'Cushion Small',                   15,   null, 116),
('Home', 'Household Linens', 'Cushion Cover Small',             8,    4,    117),
('Home', 'Household Linens', 'Cushion Cover Medium',            10,   6,    118),
('Home', 'Household Linens', 'Cushion Cover Big',               15,   8,    119),
('Home', 'Household Linens', 'Cushion Big',                     35,   null, 120),
('Home', 'Household Linens', 'Chair Cover',                     10,   5,    121),
('Home', 'Household Linens', 'Car Mat',                         25,   null, 122),
('Home', 'Household Linens', 'Bath Towel',                      8,    5,    123),
('Home', 'Household Linens', 'Bath Robe',                       18,   12,   124),
('Home', 'Household Linens', 'Bath Mat',                        15,   null, 125),

-- ---------- PART 3 · Special — Special / Delicate / Traditional Wear (22) ----------
('Special', 'Special / Delicate / Traditional Wear', 'Jalabia Normal (Traditional)', 22,  11,   126),
('Special', 'Special / Delicate / Traditional Wear', 'Mukhawara Normal',             35,  21,   127),
('Special', 'Special / Delicate / Traditional Wear', 'Mukhawara Delicate',           70,  42,   128),
('Special', 'Special / Delicate / Traditional Wear', 'Kaftan Normal',                50,  30,   129),
('Special', 'Special / Delicate / Traditional Wear', 'Kaftan Designer',              80,  48,   130),
('Special', 'Special / Delicate / Traditional Wear', 'Jalabia Normal',               12,  7.5,  131),
('Special', 'Special / Delicate / Traditional Wear', 'Woolen Bag',                   50,  null, 132),
('Special', 'Special / Delicate / Traditional Wear', 'Wedding Dress Medium',         430, null, 133),
('Special', 'Special / Delicate / Traditional Wear', 'Wedding Dress Heavy',          700, null, 134),
('Special', 'Special / Delicate / Traditional Wear', 'Trouser Leather',              125, null, 135),
('Special', 'Special / Delicate / Traditional Wear', 'Top Leather',                  100, null, 136),
('Special', 'Special / Delicate / Traditional Wear', 'Skirt Leather',                125, null, 137),
('Special', 'Special / Delicate / Traditional Wear', 'Salwar Delicate',              80,  null, 138),
('Special', 'Special / Delicate / Traditional Wear', 'Pet Bed',                      80,  null, 139),
('Special', 'Special / Delicate / Traditional Wear', 'Ladies Dress Delicate',        100, 80,   140),
('Special', 'Special / Delicate / Traditional Wear', 'Kandura Woolen',               30,  15,   141),
('Special', 'Special / Delicate / Traditional Wear', 'Jacket Leather',               150, null, 142),
('Special', 'Special / Delicate / Traditional Wear', 'Gatra Wool',                   25,  14,   143),
('Special', 'Special / Delicate / Traditional Wear', 'Flag',                         20,  null, 144),
('Special', 'Special / Delicate / Traditional Wear', 'Dress Leather',                150, null, 145),
('Special', 'Special / Delicate / Traditional Wear', 'Car Cover',                    120, null, 146),
('Special', 'Special / Delicate / Traditional Wear', 'Belt',                         50,  null, 147),

-- ---------- PART 3 · Special — Shoe Care (8) ----------
('Special', 'Shoe Care', 'Sole Protector',                      200,  null, 148),
('Special', 'Shoe Care', 'Shoe Stitching + Cleaning',           100,  null, 149),
('Special', 'Shoe Care', 'Heel Tip',                            150,  null, 150),
('Special', 'Shoe Care', 'Shoe Restoration',                    200,  null, 151),
('Special', 'Shoe Care', 'Shoe Cleaning - High-End Brand',      150,  null, 152),
('Special', 'Shoe Care', 'Shoe Cleaning - Branded',             120,  null, 153),
('Special', 'Shoe Care', 'Shoe Cleaning - Regular Brand',       100,  null, 154),
('Special', 'Shoe Care', 'Baby Shoe',                           80,   null, 155),

-- ---------- PART 3 · Special — Bag Care (8) ----------
('Special', 'Bag Care', 'Bag Restoration',                      300,  null, 156),
('Special', 'Bag Care', 'Luggage Large',                        350,  null, 157),
('Special', 'Bag Care', 'Luggage Medium',                       250,  null, 158),
('Special', 'Bag Care', 'Luggage Small',                        150,  null, 159),
('Special', 'Bag Care', 'Large Bag Cleaning',                   225,  null, 160),
('Special', 'Bag Care', 'Normal Bag Cleaning',                  200,  null, 161),
('Special', 'Bag Care', 'Medium Bag Cleaning',                  160,  null, 162),
('Special', 'Bag Care', 'Small Bag Cleaning',                   120,  null, 163)

-- Re-running the file refreshes prices instead of creating duplicates.
on conflict (category, name) do update set
  part              = excluded.part,
  clean_press_price = excluded.clean_press_price,
  press_only_price  = excluded.press_only_price,
  sort_order        = excluded.sort_order,
  is_active         = true;

notify pgrst, 'reload schema';

-- ============================================================================
-- DONE. Expect: 163 rows.
-- Check with:  select count(*) from public.pos_items;
-- ============================================================================
