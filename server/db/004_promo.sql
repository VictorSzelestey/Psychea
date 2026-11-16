-- PsyPath 1.7.0: промокоды из админки. Награда — кристаллы или предмет магазина; каждый пользователь вводит код один раз.
-- Применить от владельца базы (root) один раз, до обновления API. Данные пользователей не меняются.
begin;

create table if not exists psypath.promo_codes (
    code        text primary key,
    -- crystals — amount кристаллов, item — предмет магазина item (id из приложения)
    reward      text not null check (reward in ('crystals', 'item')),
    amount      int not null default 0 check (amount >= 0),
    item        text not null default '',
    -- null — без ограничения числа активаций
    max_uses    int check (max_uses is null or max_uses > 0),
    uses        int not null default 0,
    created_at  timestamptz not null default now(),
    created_by  bigint references psypath.users (id) on delete set null
);

create table if not exists psypath.promo_redemptions (
    code        text not null references psypath.promo_codes (code) on delete cascade,
    user_id     bigint not null references psypath.users (id) on delete cascade,
    redeemed_at timestamptz not null default now(),
    primary key (code, user_id)
);

insert into psypath.schema_version (version) values (4) on conflict do nothing;

commit;
