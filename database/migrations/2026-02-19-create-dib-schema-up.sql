-- 2026-02-19: Create isolated schema for digital-intelligence-bridge (dib)
-- Run with a privileged role (service_role/postgres) in Supabase SQL Editor.

create schema if not exists dib;

-- Allow API roles to access this schema.
grant usage on schema dib to anon, authenticated, service_role;

-- Main todo table used by the desktop app.
create table if not exists dib.todos (
    id uuid primary key default gen_random_uuid(),
    title text not null,
    description text not null default '',
    is_completed boolean not null default false,
    created_at timestamptz not null default now(),
    completed_at timestamptz null,
    priority text not null default 'normal' check (priority in ('low', 'normal', 'high')),
    category text not null default '默认',
    tags jsonb not null default '[]'::jsonb,
    due_date timestamptz null,
    updated_at timestamptz not null default now()
);

create index if not exists idx_dib_todos_created_at on dib.todos (created_at desc);
create index if not exists idx_dib_todos_is_completed on dib.todos (is_completed);

-- Keep updated_at fresh.
create or replace function dib.set_updated_at()
returns trigger
language plpgsql
as $$
begin
  new.updated_at = now();
  return new;
end;
$$;

drop trigger if exists trg_dib_todos_set_updated_at on dib.todos;
create trigger trg_dib_todos_set_updated_at
before update on dib.todos
for each row execute function dib.set_updated_at();

-- Row-level security for API access with anon key.
alter table dib.todos enable row level security;

drop policy if exists dib_todos_select_anon on dib.todos;
create policy dib_todos_select_anon on dib.todos
for select to anon, authenticated
using (true);

drop policy if exists dib_todos_insert_anon on dib.todos;
create policy dib_todos_insert_anon on dib.todos
for insert to anon, authenticated
with check (true);

drop policy if exists dib_todos_update_anon on dib.todos;
create policy dib_todos_update_anon on dib.todos
for update to anon, authenticated
using (true)
with check (true);

drop policy if exists dib_todos_delete_anon on dib.todos;
create policy dib_todos_delete_anon on dib.todos
for delete to anon, authenticated
using (true);

-- Explicit grants for table access.
grant select, insert, update, delete on table dib.todos to anon, authenticated, service_role;
