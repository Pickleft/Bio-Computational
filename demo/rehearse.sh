#!/bin/bash
B=http://127.0.0.1:8781
PASS=0; FAIL=0
say() { curl -s -X POST $B/api/say -H "Content-Type: application/json" -d "{\"text\": \"$1\"}" >/dev/null; }
snap() { for i in 1 2 3 4 5 6; do R=$(curl -s -m 8 $B/api/snapshot); if [ -n "$R" ]; then echo "$R"; return; fi; sleep 2; done; echo "$R"; }
check() { if echo "$CHAT" | grep -q "$2"; then echo "PASS: $1"; PASS=$((PASS+1)); else echo "FAIL: $1 — [$3]"; FAIL=$((FAIL+1)); fi }
echo "=== DRESS REHEARSAL v2 ==="
say "hello, my name is Jake"; sleep 9; CHAT=$(snap)
check "name learned" '"userName":"Jake"'
say "you're safe here. I'm going to take care of you"; sleep 6; CHAT=$(snap)
check "warm reply" 'Brain:'
say "let's play a game"; sleep 8; CHAT=$(snap)
check "game taught" 'made up a game'
say "sound"; sleep 5; say "light"; sleep 5; say "quiet"; sleep 9; CHAT=$(snap)
check "game scored" 'of three'
say "tell me a story"; sleep 9; CHAT=$(snap)
check "story told" 'Once there was'
say "it's time to sleep"; sleep 8; S=$(snap | grep -o '"sleepState":"[^"]*"'); echo "  sleep: $S"; CHAT=$(snap)
check "day summary at bedtime" 'Sleep now'
say "wake up"; sleep 8; CHAT=$(snap)
check "five more minutes" 'Five more minutes'
say "wake up please"; sleep 8; CHAT=$(snap)
check "grumpy wake" 'asleep'
say "tell me a secret"; sleep 8; CHAT=$(snap)
check "secret honest" 'hiding\|nothing I\|ready for that'
say "what do you remember about me?"; sleep 9; CHAT=$(snap)
check "remember-me" 'What I hold\|first thing you ever said'
say "sing for me"; sleep 8; CHAT=$(snap)
check "sing (hum or honest refusal)" 'Mmm\|feeling it\|can.t right now\|sing'
echo "=== restart continuity ==="
taskkill //f //im BioComp.Dashboard.exe >/dev/null 2>&1; sleep 2
cd /tmp/brain-demo && ./BioComp.Dashboard.exe --urls http://127.0.0.1:8781 --demo > /tmp/brain-demo/r2b.log 2>&1 &
sleep 14; CHAT=$(snap)
check "name survived" '"userName":"Jake"'
check "game record survived" '"gamesPlayed":1'
FW=$(curl -s -m 15 $B/api/farewell | grep -o '"lastWords":"[^"]*"' | head -c 260); echo "  farewell: $FW"
check "farewell carries games" 'game'
echo "=== RESULT: $PASS passed, $FAIL failed ==="
