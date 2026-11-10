-- Применить от владельца базы перед обновлением API. Данные пользователей сохраняются.
begin;

alter table psypath.users drop constraint if exists users_role_check;
alter table psypath.users add constraint users_role_check
    check (role in ('user', 'manager', 'admin'));

insert into psypath.schema_version (version) values (3) on conflict do nothing;

commit;
