-- 2026-02-19: Rollback for dib schema bootstrap

revoke select, insert, update, delete on table dib.todos from anon, authenticated, service_role;

drop policy if exists dib_todos_select_anon on dib.todos;
drop policy if exists dib_todos_insert_anon on dib.todos;
drop policy if exists dib_todos_update_anon on dib.todos;
drop policy if exists dib_todos_delete_anon on dib.todos;

drop trigger if exists trg_dib_todos_set_updated_at on dib.todos;
drop function if exists dib.set_updated_at();

drop table if exists dib.todos;

revoke usage on schema dib from anon, authenticated, service_role;
drop schema if exists dib;
