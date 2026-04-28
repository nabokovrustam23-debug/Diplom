#!/usr/bin/env bash
# Сборка пояснительной записки (ВКР) в единый .docx.
# Использование: bash docs/build-docx.sh
set -euo pipefail

ROOT="$(cd "$(dirname "$0")"/.. && pwd)"
PZ="$ROOT/docs/pz"
OUT="$ROOT/DIPLOM_ВКР_Тихий_час.docx"
REF="$ROOT/docs/reference.docx"

# Порядок объединения файлов в итоговую ПЗ:
ORDER=(
  "$PZ/titul.md"
  "$PZ/annotatsiya.md"
  "$PZ/soderzhanie.md"
  "$PZ/00-vvedenie.md"
  "$PZ/01-naznachenie.md"
  "$PZ/02-analiz.md"
  "$PZ/03-realizatsiya.md"
  "$PZ/04-testirovanie.md"
  "$PZ/05-ekspluatatsiya.md"
  "$PZ/06-zaklyuchenie.md"
  "$PZ/07-istochniki.md"
  "$PZ/prilozhenie-a.md"
  "$PZ/prilozhenie-b.md"
  "$PZ/prilozhenie-v.md"
)

MERGED="$(mktemp /tmp/vkr-merged-XXXXXX.md)"
trap 'rm -f "$MERGED"' EXIT

# Склеиваем секции с разрывами страниц перед каждым новым разделом 1-го уровня (\newpage).
{
  for i in "${!ORDER[@]}"; do
    f="${ORDER[$i]}"
    if [[ "$i" -gt 0 ]]; then
      printf '\n\n\\newpage\n\n'
    fi
    cat "$f"
  done
} > "$MERGED"

pandoc "$MERGED" \
  --from=markdown+pipe_tables+fenced_code_blocks+raw_tex+implicit_figures+raw_html \
  --to=docx \
  --reference-doc="$REF" \
  --resource-path="$PZ" \
  --highlight-style=tango \
  -o "$OUT"

echo "Готово: $OUT"
