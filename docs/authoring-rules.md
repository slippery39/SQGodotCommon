# Authoring a `.claude/rules/` file

Moved out of the root `CLAUDE.md`, which loads on every task and has a 200-line budget. This is
only needed when adding or editing a rule file.

**A rule file fails SILENTLY — it simply never loads, with no error anywhere.** Two ways, both
measured on Claude Code 2.1.116 and both now caught by the same hook:

- **CRLF in the YAML frontmatter.** The `paths:` block does not parse and the rule is inert. `.gitattributes` pins these files to LF; keep it that way.
- **`**` in a glob matches nothing.** `MtgCore/Sets/**/*.cs` matched zero files while `MtgCore/Sets/*/*.cs` matched 34. Write the levels out explicitly.

Verify a new or edited rule actually loads rather than assuming — same instinct as testing that a
card's effect fires:

```
claude -p "Read <a file the rule claims>. Then WITHOUT opening any other file: is the text of
.claude/rules/<rule>.md already in your context? Reply exactly LOADED or NOT LOADED." --allowedTools Read
```

Then repeat with a file the rule should NOT match — a rule that answers LOADED to everything has
lost its `paths:` and is costing context in every session.
