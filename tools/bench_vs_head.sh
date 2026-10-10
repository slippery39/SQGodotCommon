#!/usr/bin/env bash
# Times a test filter on HEAD vs the working tree — interleaved rounds, best of each (the machine is
# noisy: one run moves ±5%). Use it when a change touches a hot path (MoveCard, Collect, the
# state-based check, mana payment, the AI) before claiming "no slowdown".
#
#   tools/bench_vs_head.sh                     # GreedyAiTests|RandomGameTests, 3 rounds
#   tools/bench_vs_head.sh "FullyQualifiedName~RandomGameTests" 5
#
# Builds HEAD in a throwaway worktree; compares built DLLs so the build is not in the timing.
set -euo pipefail
FILTER="${1:-FullyQualifiedName~GreedyAiTests|FullyQualifiedName~RandomGameTests}"
ROUNDS="${2:-3}"
PROJECT="MagicCore.Tests"
ROOT="$(git rev-parse --show-toplevel)"
WORK="$(mktemp -d)"
trap 'git -C "$ROOT" worktree remove --force "$WORK/head" >/dev/null 2>&1 || true; rm -rf "$WORK"' EXIT

git -C "$ROOT" worktree add -q "$WORK/head" HEAD
echo "building HEAD and the working tree…"
(cd "$WORK/head" && dotnet build "$PROJECT" -v q 2>&1 | grep -E " error |Build succeeded" | sort -u)
(cd "$ROOT" && dotnet build "$PROJECT" -v q 2>&1 | grep -E " error |Build succeeded" | sort -u)
DLL="$PROJECT/bin/Debug/net10.0/$PROJECT.dll"
cp -r "$WORK/head/$PROJECT/bin/Debug/net10.0" "$WORK/head-bin"
cp -r "$ROOT/$PROJECT/bin/Debug/net10.0" "$WORK/new-bin"

declare -A best=([head]=999999 [new]=999999)
for r in $(seq 1 "$ROUNDS"); do
  for v in head new; do
    a=$(date +%s.%N)
    out=$(dotnet test "$WORK/$v-bin/$PROJECT.dll" --filter "$FILTER" 2>&1 || true)
    b=$(date +%s.%N)
    t=$(python -c "print(round($b-$a,1))")
    res=$(echo "$out" | grep -oE "(Passed|Failed)!.*Total: +[0-9]+" | head -1 | sed -E 's/ - .*//' || true)
    echo "round $r  $v  ${t}s  $res"
    best[$v]=$(python -c "print(min(${best[$v]}, $t))")
  done
done
python -c "h=${best[head]}; n=${best[new]}; print(f'best: HEAD {h}s | working tree {n}s | {100*(n-h)/h:+.1f}%')"
