#!/usr/bin/env bash
set -euo pipefail

# -----------------------------
# Config (override via env vars)
# -----------------------------
REST_URL="${REST_URL:-http://localhost:8080}"
UPLOAD_ENDPOINT="${UPLOAD_ENDPOINT:-$REST_URL/api/documents}"

DOC_NAME="${DOC_NAME:-IntegrationTest-$(date -u +%Y%m%dT%H%M%SZ)}"
TEST_FILE="${TEST_FILE:-./scripts/integration/testfile.pdf}"

ES_URL="${ES_URL:-http://localhost:9200}"
ES_INDEX="${ES_INDEX:-documents}"

POSTGRES_CONTAINER="${POSTGRES_CONTAINER:-paperless_postgres}"
POSTGRES_DB="${POSTGRES_DB:-paperless}"
POSTGRES_USER="${POSTGRES_USER:-postgres}"

MINIO_ALIAS="${MINIO_ALIAS:-minio}"
MINIO_URL_INTERNAL="${MINIO_URL_INTERNAL:-http://minio:9000}"
MINIO_BUCKET="${MINIO_BUCKET:-uploads}"
MINIO_USER="${MINIO_USER:-minioadmin}"
MINIO_PASS="${MINIO_PASS:-minioadmin}"

TIMEOUT_SECONDS="${TIMEOUT_SECONDS:-300}"
POLL_SECONDS="${POLL_SECONDS:-5}"
REST_HEALTH_PATH="${REST_HEALTH_PATH:-/health}"
REST_STABLE_SUCCESSES="${REST_STABLE_SUCCESSES:-3}"
REST_CURL_TIMEOUT="${REST_CURL_TIMEOUT:-2}"

# -----------------------------
# Helpers
# -----------------------------
require() {
  command -v "$1" >/dev/null 2>&1 || { echo "Missing required tool: $1"; exit 2; }
}

log() { echo "[E2E] $*"; }

deadline_ts() {
  date -u +%s
}

detect_compose_network() {
  # Take the first container of the compose stack and read its attached network name(s)
  local cid
  cid="$(docker compose ps -q rest 2>/dev/null || true)"

  if [[ -z "$cid" ]]; then
    # fallback: pick any container from this compose project
    cid="$(docker compose ps -q 2>/dev/null | head -n 1 || true)"
  fi

  if [[ -z "$cid" ]]; then
    echo ""
    return 1
  fi

  docker inspect -f '{{range $k, $v := .NetworkSettings.Networks}}{{println $k}}{{end}}' "$cid" | head -n 1
}

wait_for_es_ready() {
  log "Waiting for Elasticsearch cluster health to be at least yellow..."

  local start now code
  start="$(deadline_ts)"

  while true; do
    # This endpoint returns 200 only when the cluster can answer health queries.
    code="$(curl -s --max-time 2 -o /dev/null -w "%{http_code}" \
      "${ES_URL}/_cluster/health?wait_for_status=yellow&timeout=1s" || true)"
    code="${code:-000}"

    if [[ "$code" == "200" ]]; then
      log "Elasticsearch cluster health OK (>= yellow)."
      return 0
    fi

    now="$(deadline_ts)"
    if (( now - start > TIMEOUT_SECONDS )); then
      echo "ERROR: Timeout waiting for Elasticsearch cluster health (last http=$code)"
      return 1
    fi

    sleep "$POLL_SECONDS"
  done
}

# -----------------------------
# Preconditions
# -----------------------------
require docker
require curl

# jq is optional; we'll fallback to python if not installed
HAS_JQ=0
if command -v jq >/dev/null 2>&1; then HAS_JQ=1; fi
HAS_PY=0
if command -v python3 >/dev/null 2>&1; then HAS_PY=1; fi

if [[ ! -f ".env" ]]; then
  echo "ERROR: .env not found in repo root. Required for docker compose env vars."
  exit 2
fi

if [[ -z "${GEMINI_API_KEY:-}" ]]; then
  # docker compose will read GEMINI_API_KEY from .env, but for CI it is better to be explicit.
  log "Note: GEMINI_API_KEY is not in shell env. That's OK locally if docker compose reads it from .env."
fi

mkdir -p "$(dirname "$TEST_FILE")"

# Create a tiny PDF if it doesn't exist (keeps repo clean)
if [[ ! -f "$TEST_FILE" ]]; then
  log "Creating small test PDF at $TEST_FILE"
  cat > "$TEST_FILE" <<'PDF'
%PDF-1.1
1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj
2 0 obj << /Type /Pages /Kids [3 0 R] /Count 1 >> endobj
3 0 obj << /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >> endobj
4 0 obj << /Length 55 >> stream
BT /F1 18 Tf 20 100 Td (Paperless Integration Test) Tj ET
endstream endobj
5 0 obj << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> endobj
xref
0 6
0000000000 65535 f
0000000010 00000 n
0000000061 00000 n
0000000116 00000 n
0000000241 00000 n
0000000347 00000 n
trailer << /Root 1 0 R /Size 6 >>
startxref
417
%%EOF
PDF
fi

MC_CONFIG_DIR="$(mktemp -d)"
chmod 777 "$MC_CONFIG_DIR"

cleanup() {
  log "Cleaning up docker compose stack..."
  docker compose down -v --remove-orphans || true
  # Best-effort cleanup: containers might have created root-owned files
  chmod -R u+w "$MC_CONFIG_DIR" 2>/dev/null || true
  rm -rf "$MC_CONFIG_DIR" 2>/dev/null || true
}
trap cleanup EXIT

# -----------------------------
# 1) Start containers
# -----------------------------
log "Starting stack via docker compose..."
docker compose up -d --build

# Wait a short moment for ports to open
sleep 2

docker compose ps
docker network ls | grep -i default || true

COMPOSE_NETWORK="$(detect_compose_network)"
if [[ -z "$COMPOSE_NETWORK" ]]; then
  echo "ERROR: Could not detect docker compose network."
  docker compose ps
  exit 1
fi
log "Detected compose network: $COMPOSE_NETWORK"

log "Waiting for REST to be healthy at ${REST_URL}${REST_HEALTH_PATH} (need ${REST_STABLE_SUCCESSES}x HTTP 200 in a row)..."
START="$(deadline_ts)"
OK_STREAK=0

while true; do
  STATUS="$(curl -s --max-time "$REST_CURL_TIMEOUT" -o /dev/null -w "%{http_code}" "${REST_URL}${REST_HEALTH_PATH}" || true)"
  STATUS="${STATUS:-000}"

  if [[ "$STATUS" == "200" ]]; then
    OK_STREAK=$((OK_STREAK + 1))
    log "REST health OK (${OK_STREAK}/${REST_STABLE_SUCCESSES})"
    if (( OK_STREAK >= REST_STABLE_SUCCESSES )); then
      log "REST is stable (HTTP 200)."
      break
    fi
  else
    OK_STREAK=0
  fi

  NOW="$(deadline_ts)"
  if (( NOW - START > TIMEOUT_SECONDS )); then
    echo "ERROR: Timeout waiting for REST to be stable."
    log "Last REST status: $STATUS"
    log "Dumping diagnostics..."
    docker compose ps
    docker compose logs --no-color --tail=200 rabbitmq || true
    docker compose logs --no-color --tail=200 rest || true
    exit 1
  fi

  sleep "$POLL_SECONDS"
done

# -----------------------------
# 2) Upload document (multipart/form-data)
# -----------------------------
log "Uploading document to: $UPLOAD_ENDPOINT"
UPLOAD_RESPONSE="$(curl -sS -i \
  -F "Name=$DOC_NAME" \
  -F "File=@${TEST_FILE};type=application/pdf" \
  "$UPLOAD_ENDPOINT")"

HTTP_STATUS="$(echo "$UPLOAD_RESPONSE" | head -n 1 | awk '{print $2}')"
BODY="$(echo "$UPLOAD_RESPONSE" | sed -n '/^\r\{0,1\}$/,$p' | tail -n +2)"

if [[ "$HTTP_STATUS" != "201" ]]; then
  echo "ERROR: Upload failed. HTTP $HTTP_STATUS"
  echo "$BODY"
  exit 1
fi

log "Upload succeeded (HTTP 201)."

# Extract document id
DOC_ID=""
if [[ $HAS_JQ -eq 1 ]]; then
  DOC_ID="$(echo "$BODY" | jq -r '.id // .Id // empty')"
elif [[ $HAS_PY -eq 1 ]]; then
  DOC_ID="$(python3 - <<PY
import json,sys
data=json.loads(sys.stdin.read())
print(data.get("id") or data.get("Id") or "")
PY
  <<<"$BODY")"
else
  echo "ERROR: Need jq or python3 to parse JSON response for document id."
  exit 2
fi

if [[ -z "$DOC_ID" || "$DOC_ID" == "null" ]]; then
  echo "ERROR: Could not extract document id from response body:"
  echo "$BODY"
  exit 1
fi

log "DocumentId = $DOC_ID"

# Determine expected MinIO object name: {id}{ext}
EXT=".pdf"
OBJECT_NAME="${DOC_ID}${EXT}"

# -----------------------------
# 3) Verify Postgres row exists (metadata)
# -----------------------------
log "Checking Postgres: Documents row exists..."
docker exec -i "$POSTGRES_CONTAINER" psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -tAc \
  "SELECT COUNT(*) FROM \"Documents\" WHERE \"Id\"='${DOC_ID}';" | tr -d ' ' | grep -q '^1$' \
  || { echo "ERROR: Document row not found in Postgres (Documents.Id=$DOC_ID)"; exit 1; }

log "Postgres metadata OK."

# -----------------------------
# 4) Verify MinIO object exists (using minio/mc container)
# -----------------------------
log "Checking MinIO: object exists (${MINIO_BUCKET}/${OBJECT_NAME})..."

docker run --rm --network "$COMPOSE_NETWORK" \
  -v "$MC_CONFIG_DIR:/mc" \
  minio/mc:latest \
  --config-dir /mc \
  alias set "${MINIO_ALIAS}" "${MINIO_URL_INTERNAL}" "${MINIO_USER}" "${MINIO_PASS}" >/dev/null

docker run --rm --network "$COMPOSE_NETWORK" \
  -v "$MC_CONFIG_DIR:/mc" \
  minio/mc:latest \
  --config-dir /mc \
  stat "${MINIO_ALIAS}/${MINIO_BUCKET}/${OBJECT_NAME}" >/dev/null \
  || { echo "ERROR: MinIO object not found: ${MINIO_BUCKET}/${OBJECT_NAME}"; exit 1; }

log "MinIO object OK."

# -----------------------------
# 5) Wait for summary in Postgres (GenAI finished)
# -----------------------------
log "Waiting for Documents.Summary to be populated (timeout ${TIMEOUT_SECONDS}s)..."
START="$(deadline_ts)"
while true; do
  SUMMARY="$(docker exec -i "$POSTGRES_CONTAINER" psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -tAc \
    "SELECT COALESCE(\"Summary\", '') FROM \"Documents\" WHERE \"Id\"='${DOC_ID}';" | tr -d '\r')"

  if [[ -n "$SUMMARY" ]]; then
    log "Summary present (length=$(echo -n "$SUMMARY" | wc -c))."
    break
  fi

  NOW="$(deadline_ts)"
  if (( NOW - START > TIMEOUT_SECONDS )); then
    echo "ERROR: Timeout waiting for summary in Postgres for document $DOC_ID"
    log "Dumping diagnostics..."
    docker compose ps
    docker compose logs --no-color --tail=200 genaiworker || true
    docker compose logs --no-color --tail=200 rest || true
    docker compose logs --no-color --tail=200 rabbitmq || true
    exit 1
  fi

  sleep "$POLL_SECONDS"
done

# -----------------------------
# 6) Wait for Elasticsearch indexing
# -----------------------------
if ! wait_for_es_ready; then
  log "Dumping Elasticsearch diagnostics..."
  docker compose logs --no-color --tail=200 elasticsearch || true
  docker compose logs --no-color --tail=200 indexworker || true
  exit 1
fi

log "Waiting for Elasticsearch doc ($ES_INDEX/_doc/$DOC_ID) (timeout ${TIMEOUT_SECONDS}s)..."
START="$(deadline_ts)"
while true; do
  STATUS="$(curl -s -o /dev/null -w "%{http_code}" "${ES_URL}/${ES_INDEX}/_doc/${DOC_ID}" || true)"
  STATUS="${STATUS:-000}"
  if [[ "$STATUS" == "200" ]]; then
    log "Elasticsearch indexing OK."
    break
  fi

  NOW="$(deadline_ts)"
  if (( NOW - START > TIMEOUT_SECONDS )); then
    echo "ERROR: Timeout waiting for Elasticsearch doc for $DOC_ID (last status=$STATUS)"
    log "Dumping diagnostics..."
    docker compose ps
    docker compose logs --no-color --tail=200 elasticsearch || true
    docker compose logs --no-color --tail=200 indexworker || true
    docker compose logs --no-color --tail=200 rabbitmq || true
    exit 1
  fi

  sleep "$POLL_SECONDS"
done

log "E2E upload integration test PASSED."
