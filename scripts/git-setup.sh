#!/usr/bin/env bash
# =============================================
# CROSSROADS repo setup helper — safe to re-run anytime.
#
# Usage:
#   ./scripts/git-setup.sh ["Your Name"] ["you@example.com"] ["https://github.com/<you>/<repo>.git"]
#
# Notes:
# - In the sandboxed workspace, .git/config does NOT persist between sessions,
#   so re-run this script if commits start failing with "please tell me who you are"
#   or if the 'origin' remote disappears.
# =============================================
set -euo pipefail
cd "$(dirname "$0")/.."

NAME="${1:-Crossroads Dev}"
EMAIL="${2:-dev@crossroads.local}"
REMOTE_URL="${3:-}"

echo "== Repository: $(basename "$PWD") =="

git config user.name  "$NAME"
git config user.email "$EMAIL"
echo "Identity set: $NAME <$EMAIL>"

if command -v git-lfs >/dev/null 2>&1; then
  git lfs install --local >/dev/null 2>&1 || git lfs install >/dev/null
  echo "Git LFS filters active."
else
  echo "WARNING: git-lfs is not installed on this machine."
  echo "         .gitattributes LFS rules will activate once it is installed"
  echo "         (install BEFORE committing binary art/audio assets)."
fi

if [ -n "$REMOTE_URL" ]; then
  if git remote get-url origin >/dev/null 2>&1; then
    git remote set-url origin "$REMOTE_URL"
  else
    git remote add origin "$REMOTE_URL"
  fi
  echo "Remote 'origin' -> $REMOTE_URL"
fi

echo
echo "Current branch: $(git branch --show-current 2>/dev/null || echo 'HEAD detached')"
git log --oneline -n 5 2>/dev/null || echo "(no commits yet)"
echo
echo "Done."
