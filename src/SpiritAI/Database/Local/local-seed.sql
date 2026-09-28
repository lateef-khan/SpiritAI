-- Made-up users for the throwaway database only, one per access group. Never run this on Neon.
-- Their roles come from the seed script, pointed at the local database.
INSERT INTO neon_auth."user" (id, name, email, "emailVerified") VALUES
    ('00000000-0000-4000-8000-000000000001', 'Local Guest',                   'guest@local.test',                   true),
    ('00000000-0000-4000-8000-000000000002', 'Local Dealer',                  'dealer@local.test',                  true),
    ('00000000-0000-4000-8000-000000000003', 'Local Tech Service',            'tech-service@local.test',            true),
    ('00000000-0000-4000-8000-000000000004', 'Local Inside Sales',            'inside-sales@local.test',            true),
    ('00000000-0000-4000-8000-000000000005', 'Local Inside Sales Supervisor', 'inside-sales-supervisor@local.test', true),
    ('00000000-0000-4000-8000-000000000006', 'Local Tech Service Manager',    'tech-service-manager@local.test',    true),
    ('00000000-0000-4000-8000-000000000007', 'Local Inside Sales Manager',    'inside-sales-manager@local.test',    true),
    ('00000000-0000-4000-8000-000000000008', 'Local Admin',                   'admin@local.test',                   true)
ON CONFLICT (id) DO NOTHING;
