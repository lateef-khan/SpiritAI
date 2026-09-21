-- Development data for the throwaway PostgreSQL that `just run` starts.
--
-- Two things the app cannot make on its own locally:
--   1. neon_auth."user" — on Neon this table belongs to Neon Auth. Staff is whoever has a row in
--      it, so without one every /v1/handoff call answers 500. Here it is a plain table with the
--      one signed-in developer, passed in as :staff_email and :staff_name.
--   2. A few handoffs, so the inbox has rows in every state.
--
-- Every insert is idempotent: run it as often as you like.

CREATE SCHEMA IF NOT EXISTS neon_auth;

CREATE TABLE IF NOT EXISTS neon_auth."user" (
    id     uuid PRIMARY KEY,
    name   text NOT NULL,
    email  text NOT NULL,
    banned boolean
);

INSERT INTO neon_auth."user" (id, name, email)
VALUES ('00000000-0000-4000-8000-000000000001', :'staff_name', :'staff_email')
ON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name, email = EXCLUDED.email;

-- One chat: the conversation, its visitor, the visitor's first message.
CREATE OR REPLACE FUNCTION pg_temp.seed_chat(p_conversation_id text, p_visitor text, p_text text, p_at timestamptz)
RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    INSERT INTO agentcore.conversation (conversation_id, title, status, external_id, custom, state, next_ordinal, created_at, updated_at)
    VALUES (p_conversation_id, NULL, 'regular', NULL, jsonb_build_object('owner', 'visitor:' || p_visitor), NULL, 1, p_at, p_at)
    ON CONFLICT (conversation_id) DO NOTHING;

    INSERT INTO agentcore.conversation_principal (conversation_id, principal_key, role, attached_at)
    VALUES (p_conversation_id, 'visitor:' || p_visitor, 'visitor', p_at)
    ON CONFLICT DO NOTHING;

    INSERT INTO agentcore.conversation_message (conversation_id, ordinal, turn_index, role, content, message_id, created_at, updated_at)
    VALUES (p_conversation_id, 0, 0, 'user',
        jsonb_build_object('role', 'user', 'contents', jsonb_build_array(jsonb_build_object('$type', 'text', 'text', p_text))),
        md5(p_conversation_id || ':0'), p_at, p_at)
    ON CONFLICT DO NOTHING;
END $$;

DO $$
BEGIN
    PERFORM pg_temp.seed_chat('a4175d6a463c4671b9d2658d0d4c6840', '07056b6e-13ae-4527-bb7b-b71c68d42003',
    'My CT800 belt slips at 8 mph. It''s about 2 years old.', now() - interval '22 minutes');
    PERFORM pg_temp.seed_chat('9f65feb9bb8c430b80d2e0a3cc948fe0', 'a27e5537-ead1-494d-88c9-866a39c9c36e',
    'Error E7 on XE395, what does it mean?', now() - interval '9 minutes');
    PERFORM pg_temp.seed_chat('5b480533454d49378509adbcbbdbb7c2', '806d5174-f122-41e2-9c14-d10e56e37dc4',
    'We''re a dealer in Tacoma. What mat do you ship with the CT900 now?', now() - interval '3 minutes');
    PERFORM pg_temp.seed_chat('c1d2e3f4a5b6c7d8e9f0a1b2c3d4e5f6', '3b1f0f0e-2d61-4d3c-9d0b-1c1a9b1c2d3e',
    'Bluetooth will not pair on my XBR95.', now() - interval '31 minutes');
    PERFORM pg_temp.seed_chat('d4c3b2a1f6e5d4c3b2a1f0e9d8c7b6a5', '9d8c7b6a-5f4e-4d3c-8b2a-1f0e9d8c7b6a',
    'Can you track order 48213 for me?', now() - interval '2 hours');
END $$;

-- The handoffs. Three waiting, one taken by a colleague, one closed.
INSERT INTO spirit.handoff (conversation_id, status, asked_by, reason, asked_at, assignee_key, assignee_name, claimed_at, email, done_at)
SELECT v.*
FROM (VALUES
    ('a4175d6a463c4671b9d2658d0d4c6840', 'waiting', 'bot',
        'Warranty claim. The visitor tried the belt tension steps twice.', now() - interval '14 minutes',
        NULL, NULL, NULL, 'lorrie@northwind.example', NULL),
    ('9f65feb9bb8c430b80d2e0a3cc948fe0', 'waiting', 'visitor',
        NULL, now() - interval '9 minutes',
        NULL, NULL, NULL, NULL, NULL),
    ('5b480533454d49378509adbcbbdbb7c2', 'waiting', 'bot',
        'Dealer pricing. I have no price list.', now() - interval '3 minutes',
        NULL, NULL, NULL, NULL, NULL),
    ('c1d2e3f4a5b6c7d8e9f0a1b2c3d4e5f6', 'human', 'visitor',
        NULL, now() - interval '30 minutes',
        'user:dev-marco', 'Marco', now() - interval '26 minutes', 'priya.n@example.com', NULL),
    ('d4c3b2a1f6e5d4c3b2a1f0e9d8c7b6a5', 'done', 'visitor',
        NULL, now() - interval '2 hours',
        'user:dev-marco', 'Marco', now() - interval '116 minutes', NULL, now() - interval '110 minutes')
) AS v(conversation_id, status, asked_by, reason, asked_at, assignee_key, assignee_name, claimed_at, email, done_at)
WHERE NOT EXISTS (SELECT 1 FROM spirit.handoff h WHERE h.conversation_id = v.conversation_id);
