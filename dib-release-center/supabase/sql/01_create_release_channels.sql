-- 01_create_release_channels.sql
-- 使用现有 dib schema 风格创建发布中心基础表。

create extension if not exists pgcrypto;

create schema if not exists dib_release;

grant usage on schema dib_release to anon, authenticated, service_role;

-- 统一维护 updated_at。
create or replace function dib_release.set_updated_at()
returns trigger
language plpgsql
as $$
begin
  new.updated_at = now();
  return new;
end;
$$;

create table if not exists dib_release.release_channels (
    id uuid primary key default gen_random_uuid(),
    channel_code text not null unique check (channel_code ~ '^[a-z][a-z0-9-]*$'),
    channel_name text not null,
    description text not null default '',
    sort_order integer not null check (sort_order > 0),
    is_default boolean not null default false,
    is_active boolean not null default true,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create index if not exists idx_release_channels_sort_order
    on dib_release.release_channels (sort_order asc);

create unique index if not exists ux_release_channels_default_true
    on dib_release.release_channels (is_default)
    where is_default;

drop trigger if exists trg_release_channels_set_updated_at on dib_release.release_channels;
create trigger trg_release_channels_set_updated_at
before update on dib_release.release_channels
for each row execute function dib_release.set_updated_at();

grant select on table dib_release.release_channels to anon, authenticated, service_role;
grant insert, update, delete on table dib_release.release_channels to authenticated, service_role;
