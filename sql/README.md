# SQL

## Decisions

P3 (2026-10-01): SerialInvoices=13206, WithOwnContact=459 over the last 12 months. P3: SKIP my_invoice
  (459 is under one in ten; guests get CustService order amounts through my_unit, no Sage invoice).

## What is here

| File | Run on | As | What it does |
| --- | --- | --- | --- |
| `custservice/logins.sql` | SPIRITSRV-024 | sysadmin | logins, users, roles, schemas, grants, cost DENYs on CustService |
| `sage/logins.sql` | SPIRITSRV-021 | sysadmin | the same on Sage `MAS_SFC` |
| `custservice/agent-procs.sql` | SPIRITSRV-024 | db_owner of CustService | the `core` and `agent` objects on CustService |
| `sage/agent-procs.sql` | SPIRITSRV-021 | db_owner of MAS_SFC | the `core` and `agent` objects on Sage |
| `checks/run.sh` | the laptop | the three new logins | proves all of it; prints PASS and FAIL only (19 checks in `logins`) |

Every file is safe to run again.

## Order

1. `logins.sql` on both servers, with the passwords below.
2. `agent-procs.sql` on both servers.
3. `checks/run.sh` after each step, as the plan says.

## Passwords

The owner makes six passwords and keeps them in `secrets/dev.env` and `secrets/prod.env`. Each is
40 letters and digits. The DBA gets them from the owner by the password manager, never by email.

| sqlcmd variable | CustService key | Sage key |
| --- | --- | --- |
| `DabPassword` | `DAB_CUSTSERVICE_SQL_PASSWORD` | `DAB_SAGE_SQL_PASSWORD` |
| `ManagerPassword` | `CUSTSERVICE_SQL_MANAGER_PASSWORD` | `SAGE_SQL_MANAGER_PASSWORD` |
| `AdminPassword` | `CUSTSERVICE_SQL_ADMIN_PASSWORD` | `SAGE_SQL_ADMIN_PASSWORD` |

sqlcmd reads environment variables as scripting variables, so the passwords never go on a command
line. In PowerShell on each server, set the three, run the script, then remove them. Type the
CustService passwords on SPIRITSRV-024 and the Sage passwords on SPIRITSRV-021. `-AsSecureString`
keeps what you type off the screen.

First check the compatibility level of both databases. The checks use `STRING_SPLIT`, which needs
level 130 or higher; below that, the two count checks FAIL with error 208:

```
SELECT name, compatibility_level FROM sys.databases WHERE name IN ('CustService','MAS_SFC');
```

Then:

```
$env:DabPassword = [Net.NetworkCredential]::new('', (Read-Host -AsSecureString 'spiritai_dab')).Password
$env:ManagerPassword = [Net.NetworkCredential]::new('', (Read-Host -AsSecureString 'spiritai_manager')).Password
$env:AdminPassword = [Net.NetworkCredential]::new('', (Read-Host -AsSecureString 'spiritai_admin')).Password
sqlcmd -S SPIRITSRV-024 -E -C -b -i custservice\logins.sql
Remove-Item Env:DabPassword, Env:ManagerPassword, Env:AdminPassword
```

Then the same with `-S SPIRITSRV-021 -i sage\logins.sql` and the Sage passwords. Then, with no
variables:

```
sqlcmd -S SPIRITSRV-024 -E -C -b -i custservice\agent-procs.sql
sqlcmd -S SPIRITSRV-021 -E -C -b -i sage\agent-procs.sql
```

## After a Sage upgrade or restore

A Sage upgrade or a restore of `MAS_SFC` can drop our schemas and logins' users. Run
`sage/logins.sql` and `sage/agent-procs.sql` again, then `checks/run.sh logins` and
`checks/run.sh sage`.

## After a new column

A new column whose name contains `Cost`, or a new view that reads one, is open to the manager
until `logins.sql` runs again on that server. Run it after any schema change.
