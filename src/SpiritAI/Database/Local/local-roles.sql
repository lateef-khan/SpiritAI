-- One local role per old access group, holding the permissions the AccessModel migration gives
-- that group, for the made-up users of local-seed.sql. The local Admin gets the built-in Admin
-- role. Does nothing until the app has migrated once: db-up runs before the first migrate.
DO $$
BEGIN
    IF to_regclass('spirit.role_permission') IS NULL THEN
        RAISE NOTICE 'spirit.role_permission does not exist yet. Start the app once, then run: just spirit seed-local';
        RETURN;
    END IF;

    DELETE FROM spirit.role
    WHERE name LIKE 'local-%'
      AND id NOT IN (
        '00000000-0000-4000-8001-000000000001', '00000000-0000-4000-8001-000000000002',
        '00000000-0000-4000-8001-000000000003', '00000000-0000-4000-8001-000000000004',
        '00000000-0000-4000-8001-000000000005', '00000000-0000-4000-8001-000000000006',
        '00000000-0000-4000-8001-000000000007');

    INSERT INTO spirit.role (id, name, description, built_in) VALUES
        ('00000000-0000-4000-8001-000000000001', 'local-Guest',                 'Local only.', false),
        ('00000000-0000-4000-8001-000000000002', 'local-Dealer',                'Local only.', false),
        ('00000000-0000-4000-8001-000000000003', 'local-TechService',           'Local only.', false),
        ('00000000-0000-4000-8001-000000000004', 'local-InsideSales',           'Local only.', false),
        ('00000000-0000-4000-8001-000000000005', 'local-InsideSalesSupervisor', 'Local only.', false),
        ('00000000-0000-4000-8001-000000000006', 'local-TechServiceManager',    'Local only.', false),
        ('00000000-0000-4000-8001-000000000007', 'local-InsideSalesManager',    'Local only.', false)
    ON CONFLICT (id) DO NOTHING;

    -- Desk and CRM come from a person's link, not a permission; no hub.* key belongs on a local role.
    DELETE FROM spirit.role_permission WHERE permission LIKE 'hub.%' AND role_id::text LIKE '00000000-0000-4000-8001-%';

    INSERT INTO spirit.role_permission (role_id, permission)
    SELECT r.id, p.permission
    FROM (VALUES
        ('00000000-0000-4000-8001-000000000001'::uuid, ARRAY['chat.agent.guest']),
        ('00000000-0000-4000-8001-000000000002'::uuid, ARRAY['chat.agent.dealer']),
        ('00000000-0000-4000-8001-000000000003'::uuid, ARRAY['chat.agent.staff', 'lookup.units', 'lookup.orders']),
        ('00000000-0000-4000-8001-000000000004'::uuid, ARRAY['chat.agent.staff', 'lookup.units', 'lookup.orders']),
        ('00000000-0000-4000-8001-000000000005'::uuid, ARRAY['chat.agent.staff', 'lookup.units', 'lookup.orders']),
        ('00000000-0000-4000-8001-000000000006'::uuid, ARRAY['chat.agent.manager', 'lookup.units', 'lookup.orders']),
        ('00000000-0000-4000-8001-000000000007'::uuid, ARRAY['chat.agent.manager', 'lookup.units', 'lookup.orders'])
    ) AS r(id, permissions)
    CROSS JOIN LATERAL unnest(r.permissions) AS p(permission)
    ON CONFLICT DO NOTHING;

    INSERT INTO spirit.user_role (user_id, role_id) VALUES
        ('00000000-0000-4000-8000-000000000001', '00000000-0000-4000-8001-000000000001'),
        ('00000000-0000-4000-8000-000000000002', '00000000-0000-4000-8001-000000000002'),
        ('00000000-0000-4000-8000-000000000003', '00000000-0000-4000-8001-000000000003'),
        ('00000000-0000-4000-8000-000000000004', '00000000-0000-4000-8001-000000000004'),
        ('00000000-0000-4000-8000-000000000005', '00000000-0000-4000-8001-000000000005'),
        ('00000000-0000-4000-8000-000000000006', '00000000-0000-4000-8001-000000000006'),
        ('00000000-0000-4000-8000-000000000007', '00000000-0000-4000-8001-000000000007'),
        ('00000000-0000-4000-8000-000000000008', 'a0000000-0000-4000-8000-000000000001')
    ON CONFLICT DO NOTHING;
END $$;
