#!/bin/sh
# Database per service: one PostgreSQL server, one database each, created the first time the data
# volume is initialised. The Aspire AppHost does the same with AddDatabase(...).
set -e

for database in catalogdb orderingdb inventorydb notificationsdb; do
    psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_USER" \
        -c "CREATE DATABASE \"$database\";"
done
