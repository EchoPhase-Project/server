# Database changes

This file tells how to change the Postgres model and the Scylla schema.

**Postgres.** Change the model in `DAL.Postgres`. Then run:

```sh
mise run migrations:add <Name>
```

`migrations:check` fails if the model has changes that no migration includes.
Do not edit a released migration. Add a new migration.

**Scylla.** Write migrations manually in `DAL.Scylla/Migrations/`. Each
migration is one class `M<yyyyMMdd><Name>` that implements `IMigration`. Give
it a unique `Id` that sorts after the current `Id` values. CQL schema changes
are not transactional. Thus, `Down` must undo a partial `Up` (`IF EXISTS`).

The `migrate` command applies the two types of migrations. The chart runs
this command before the server starts.
