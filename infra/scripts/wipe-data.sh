#!/usr/bin/env bash
# Wipe all orders and customers from PostgreSQL, Qdrant collections, and Redis projections.
# Run from repo root. Requires: Docker with playground-postgres, playground-qdrant and playground-redis running.
# Usage: ./infra/scripts/wipe-data.sh

set -e

echo "=== Wipe data (PostgreSQL + Qdrant + Redis) ==="

# --- PostgreSQL ---
echo ""
echo "PostgreSQL: deleting orders and customers..."
docker exec -i playground-postgres psql -U playground -d playground_db -v ON_ERROR_STOP=1 <<'SQL'
DELETE FROM ordering.order_lines;
DELETE FROM ordering.orders;
DELETE FROM customers.customers;
SQL
echo "PostgreSQL: orders and customers deleted."

# --- Qdrant ---
echo ""
echo "Qdrant: deleting collections 'orders' and 'customers'..."
for coll in orders customers; do
  if curl -s -X DELETE "http://localhost:6333/collections/$coll" -o /dev/null -w "%{http_code}" | grep -q 200; then
    echo "  Deleted collection: $coll"
  else
    echo "  Collection $coll did not exist or error (skip)."
  fi
done
# --- Redis ---
echo ""
echo "Redis: flushing projection keys..."
DELETED=$(docker exec playground-redis redis-cli EVAL "local keys = redis.call('KEYS','projections:*'); if #keys > 0 then return redis.call('DEL', unpack(keys)) else return 0 end" 0 2>/dev/null || echo "ERROR")
if [ "$DELETED" = "ERROR" ]; then
  echo "  Redis not reachable. Is playground-redis running? (docker ps)"
elif [ "$DELETED" -gt 0 ] 2>/dev/null; then
  echo "  Deleted $DELETED projection keys."
else
  echo "  No projection keys found (skip)."
fi

echo ""
echo "Done. PostgreSQL, Qdrant and Redis data wiped."