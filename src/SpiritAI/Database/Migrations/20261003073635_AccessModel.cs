using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpiritAI.Database.Migrations
{
    /// <inheritdoc />
    public partial class AccessModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE spirit.role ADD COLUMN id uuid NOT NULL DEFAULT gen_random_uuid();
                ALTER TABLE spirit.role ADD COLUMN description text NULL;
                ALTER TABLE spirit.role ADD COLUMN built_in boolean NOT NULL DEFAULT false;
                """);

            // The built-in role takes the name Admin, so whatever holds that name now steps aside.
            // user_role follows a rename: its key to role.name cascades on update.
            migrationBuilder.Sql("""
                UPDATE spirit.role SET name = name || ' (old ' || id || ')' WHERE lower(name) = 'admin';

                INSERT INTO spirit.role (id, name, description, built_in, access_group)
                VALUES ('a0000000-0000-4000-8000-000000000001', 'Admin', 'Every permission. Built in.', true, NULL);

                INSERT INTO spirit.user_role (user_id, role)
                SELECT DISTINCT ur.user_id, 'Admin'
                FROM spirit.user_role ur JOIN spirit.role r ON r.name = ur.role
                WHERE r.access_group = 'Admin'
                ON CONFLICT DO NOTHING;

                DELETE FROM spirit.user_role WHERE role IN (SELECT name FROM spirit.role WHERE access_group = 'Admin');
                DELETE FROM spirit.role WHERE access_group = 'Admin';
                """);

            migrationBuilder.Sql("""
                CREATE TABLE spirit.role_permission (
                    role_id    uuid NOT NULL,
                    permission text NOT NULL,
                    CONSTRAINT "PK_role_permission" PRIMARY KEY (role_id, permission)
                );

                INSERT INTO spirit.role_permission (role_id, permission)
                SELECT r.id, p.permission
                FROM spirit.role r
                CROSS JOIN LATERAL unnest(CASE r.access_group
                    WHEN 'Guest' THEN ARRAY['chat.agent.guest']
                    WHEN 'Dealer' THEN ARRAY['chat.agent.dealer']
                    WHEN 'TechService' THEN ARRAY['chat.agent.staff', 'lookup.units', 'lookup.orders']
                    WHEN 'InsideSales' THEN ARRAY['chat.agent.staff', 'lookup.units', 'lookup.orders']
                    WHEN 'InsideSalesSupervisor' THEN ARRAY['chat.agent.staff', 'lookup.units', 'lookup.orders']
                    WHEN 'TechServiceManager' THEN ARRAY['chat.agent.manager', 'lookup.units', 'lookup.orders']
                    WHEN 'InsideSalesManager' THEN ARRAY['chat.agent.manager', 'lookup.units', 'lookup.orders']
                    ELSE ARRAY[]::text[]
                END) AS p(permission);
                """);

            migrationBuilder.Sql("""
                ALTER TABLE spirit.user_role ADD COLUMN role_id uuid NULL;
                UPDATE spirit.user_role ur SET role_id = r.id FROM spirit.role r WHERE r.name = ur.role;

                ALTER TABLE spirit.user_role DROP CONSTRAINT "PK_user_role";
                ALTER TABLE spirit.user_role DROP CONSTRAINT "FK_user_role_role_role";
                DROP INDEX spirit."IX_user_role_role";
                ALTER TABLE spirit.user_role DROP COLUMN role;
                ALTER TABLE spirit.user_role ALTER COLUMN role_id SET NOT NULL;

                ALTER TABLE spirit.role DROP CONSTRAINT "PK_role";
                ALTER TABLE spirit.role ALTER COLUMN id DROP DEFAULT;
                ALTER TABLE spirit.role ADD CONSTRAINT "PK_role" PRIMARY KEY (id);
                ALTER TABLE spirit.role DROP CONSTRAINT role_access_group_check;
                ALTER TABLE spirit.role DROP COLUMN access_group;

                ALTER TABLE spirit.user_role ADD CONSTRAINT "PK_user_role" PRIMARY KEY (user_id, role_id);
                ALTER TABLE spirit.user_role ADD CONSTRAINT "FK_user_role_role_role_id"
                    FOREIGN KEY (role_id) REFERENCES spirit.role (id) ON DELETE CASCADE;
                CREATE INDEX "IX_user_role_role_id" ON spirit.user_role (role_id);

                ALTER TABLE spirit.role_permission ADD CONSTRAINT "FK_role_permission_role_role_id"
                    FOREIGN KEY (role_id) REFERENCES spirit.role (id) ON DELETE CASCADE;

                CREATE UNIQUE INDEX "IX_role_name_lower" ON spirit.role (lower(name));
                CREATE UNIQUE INDEX "IX_role_built_in" ON spirit.role (built_in) WHERE built_in;
                """);

            migrationBuilder.Sql("""
                CREATE TABLE spirit.person_ban (
                    user_id   uuid        NOT NULL,
                    banned_at timestamptz NOT NULL,
                    banned_by uuid        NULL,
                    reason    text        NULL,
                    CONSTRAINT "PK_person_ban" PRIMARY KEY (user_id),
                    CONSTRAINT "FK_person_ban_user_user_id" FOREIGN KEY (user_id)
                        REFERENCES neon_auth."user" (id) ON DELETE CASCADE
                );

                INSERT INTO spirit.person_ban (user_id, banned_at, banned_by, reason)
                SELECT id, now(), NULL, 'Copied from Neon' FROM neon_auth."user" WHERE banned = true;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
            => throw new NotSupportedException(
                "AccessModel moves access groups into permissions and cannot be undone. Restore the Postgres backup taken before the release.");
    }
}
