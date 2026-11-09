-- PsyPath: аккаунты, прогресс, результаты, контент для админа.
-- Всё в отдельной схеме psypath. Применять от владельца базы (root) один раз.

create schema psypath;

create table psypath.schema_version (
    version    int primary key,
    applied_at timestamptz not null default now()
);

create table psypath.users (
    id            bigint generated always as identity primary key,
    login         text not null check (length(login) between 3 and 32),
    password_hash text not null,                  -- bcrypt, проверяет API
    display_name  text not null default '',
    role          text not null default 'user' check (role in ('user', 'admin')),
    is_blocked    boolean not null default false,
    created_at    timestamptz not null default now(),
    last_login_at timestamptz
);
-- логин уникален без учёта регистра
create unique index users_login_uq on psypath.users (lower(login));

-- прогресс пользователя: числа для статистики отдельными столбцами,
-- остальное (пройденные уроки, достижения, цель дня…) — как ProgressData в data
create table psypath.progress (
    user_id     bigint primary key references psypath.users (id) on delete cascade,
    rating      double precision not null default 100,
    total_xp    int not null default 0,
    streak      int not null default 0,
    last_active date,
    answered    int not null default 0,
    correct     int not null default 0,
    data        jsonb not null default '{}'::jsonb,
    updated_at  timestamptz not null default now()
);

create table psypath.lesson_results (
    id          bigint generated always as identity primary key,
    user_id     bigint not null references psypath.users (id) on delete cascade,
    lesson_id   text not null,
    accuracy    int not null check (accuracy between 0 and 100),
    mistakes    int not null default 0,
    xp          int not null default 0,
    finished_at timestamptz not null default now()
);
create index lesson_results_user_idx on psypath.lesson_results (user_id);
create index lesson_results_lesson_idx on psypath.lesson_results (lesson_id);

create table psypath.questionnaire_results (
    id               bigint generated always as identity primary key,
    user_id          bigint not null references psypath.users (id) on delete cascade,
    questionnaire_id text not null,
    headline         text not null,
    scores           jsonb not null default '[]'::jsonb,   -- [{"scale": "...", "value": 5, "max": 10}]
    taken_at         timestamptz not null default now()
);
create index questionnaire_results_user_idx on psypath.questionnaire_results (user_id);
create index questionnaire_results_q_idx on psypath.questionnaire_results (questionnaire_id);

-- контент, который правит админ: те же JSON, что лежат в Resources/Raw
create table psypath.content (
    name       text primary key check (name in ('lessons', 'questionnaires', 'rating', 'reference')),
    data       jsonb not null,
    version    int not null default 1,
    updated_at timestamptz not null default now(),
    updated_by bigint references psypath.users (id) on delete set null
);

-- прошлые версии контента, чтобы откатить неудачную правку
create table psypath.content_history (
    id         bigint generated always as identity primary key,
    name       text not null,
    version    int not null,
    data       jsonb not null,
    changed_at timestamptz not null default now(),
    changed_by bigint references psypath.users (id) on delete set null
);
create index content_history_name_idx on psypath.content_history (name, version desc);

-- статистика для админа
create view psypath.v_stats_overview as
select
    (select count(*) from psypath.users where role = 'user')                              as users_total,
    (select count(*) from psypath.progress where last_active >= current_date - 7)         as active_7d,
    (select round(avg(p.rating)::numeric, 1)
       from psypath.progress p join psypath.users u on u.id = p.user_id
      where u.role = 'user')                                                              as avg_rating,
    (select count(*) from psypath.lesson_results)                                         as lessons_finished,
    (select count(*) from psypath.questionnaire_results)                                  as questionnaires_taken;

create view psypath.v_lesson_stats as
select lesson_id,
       count(*)                          as attempts,
       count(distinct user_id)           as users,
       round(avg(accuracy)::numeric, 1)  as avg_accuracy
from psypath.lesson_results
group by lesson_id;

create view psypath.v_questionnaire_stats as
select questionnaire_id, headline, count(*) as times
from psypath.questionnaire_results
group by questionnaire_id, headline;

insert into psypath.schema_version (version) values (1);
