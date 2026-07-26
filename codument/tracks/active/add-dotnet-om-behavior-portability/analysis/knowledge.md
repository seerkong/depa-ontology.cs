# Knowledge

- JSON owns manifest semantics; adapters must converge before validation/import.
- Metadata presence and callback executability are distinct facts.
- `requireReady` is a preflight plus atomic apply contract, not a post-import validation flag.
- Script source is not a callback binding and remains outside Om.Core.
