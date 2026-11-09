-- PsyPath: пользователь может скрыть себя из топа.
-- Применять от владельца базы (root) один раз, до обновления API.

alter table psypath.users add column hide_in_top boolean not null default false;

insert into psypath.schema_version (version) values (2);
