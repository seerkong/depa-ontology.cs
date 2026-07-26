# P2 Coding Attractor Round 1

## Result

GAP

## Evidence

Import preflight accepted legal constraint type names and non-empty callback arrays but did not require the slots needed by that type. A conditional constraint with only a validator could therefore pass strict import and later be skipped by the runtime's non-custom constraint path.

## Applied Revision

Added T2.4 to enforce exact constraint type/slot coherence before effects and to prove legal conditional and custom callbacks actually execute after import.
