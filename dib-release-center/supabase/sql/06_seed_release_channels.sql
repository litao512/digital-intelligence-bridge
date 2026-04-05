-- 06_seed_release_channels.sql
-- 初始化默认渠道。

insert into dib_release.release_channels (
    channel_code,
    channel_name,
    description,
    sort_order,
    is_default,
    is_active
)
values
    ('stable', '稳定版', '面向正式发布的默认渠道。', 10, true, true),
    ('beta', '测试版', '面向试用与预发布验证的渠道。', 20, false, true),
    ('internal', '内部版', '面向内部联调与快速验证的渠道。', 30, false, true)
on conflict (channel_code) do update
set channel_name = excluded.channel_name,
    description = excluded.description,
    sort_order = excluded.sort_order,
    is_default = excluded.is_default,
    is_active = excluded.is_active;
