# The agent-skill recipes: a thin wrapper over the standard skills CLI (`bun x skills@1` by default).
#
# The work is done by skills.mts, beside this file, which needs no .NET at all - no solution to
# evaluate, no restore and no build - so a repository whose build is broken can still install the
# skills that might help fix it. It runs under Bun, then Deno, and falls back to Node with the
# generated skills.mjs. Set PURVIEW_SKILLS_RUNTIME to choose, for example `node`, `bun` or `deno run -A`.
#
# Every recipe here is run by a person, on purpose. Never make one a dependency of build, test or
# default, or call skills-add, -install, -update or -fix from a git hook: they change the repository.
# skills-check is read-only.
#
# Settings live in the "Skills" section of .config/purview-build-sdk.json:
#
#   { "Skills": { "Agents": ["claude-code", "codex"], "Runner": "bun" } }
#
# Pass a one-off through *args, for example `just skills-update --dry-run --agents claude-code,codex`,
# or set the matching PURVIEW_SKILLS_* environment variable.

skills_script := source_directory() / "skills"

# Runs skills.mts (or skills.mjs) from the repository root with the given arguments.
_skills_run := 'purview_skills() { if [ -n "${PURVIEW_SKILLS_RUNTIME:-}" ]; then case "$PURVIEW_SKILLS_RUNTIME" in node*) $PURVIEW_SKILLS_RUNTIME "' + skills_script + '.mjs" "$@" ;; *) $PURVIEW_SKILLS_RUNTIME "' + skills_script + '.mts" "$@" ;; esac; elif command -v bun >/dev/null 2>&1; then bun "' + skills_script + '.mts" "$@"; elif command -v deno >/dev/null 2>&1; then deno run -A "' + skills_script + '.mts" "$@"; elif command -v node >/dev/null 2>&1; then node "' + skills_script + '.mjs" "$@"; else echo "error PRSKL0004: the skills recipes need Bun, Deno or Node.js on PATH." >&2; return 1; fi; }; purview_skills'

# Install skills from another repository through the skills CLI (append '#<ref>' to pin it)
#
#   just skills-add purview-dev/engineering-playbook
[doc("Install skills from another repository through the skills CLI (append '#<ref>' to pin it)")]
[group('Agent Skills')]
skills-add source *args:
    echo "==> Installing skills from {{ BLUE }}{{ source }}{{ NORMAL }}"
    {{ _skills_run }} add {{ quote(source) }} --root "{{ justfile_directory() }}" {{ args }}

# Restore every skill skills-lock.json records - how a fresh clone gets the repository's skills back
[group('Agent Skills')]
skills-install *args:
    {{ _skills_run }} install --root "{{ justfile_directory() }}" {{ args }}

# Update the agent skills by re-adding every unique source in skills-lock.json
#
# Each source is re-added at its latest with every skill it has, which refreshes the installed skills
# and adds any a source has gained; `skills update` is never run. skills-fix then removes links from
# agent directories Skills.Agents does not select, and does nothing when it is unset.
#
#   just skills-update --dry-run
[doc('Update the agent skills by re-adding every unique source in skills-lock.json')]
[group('Agent Skills')]
skills-update *args:
    {{ _skills_run }} update --root "{{ justfile_directory() }}" {{ args }}
    {{ _skills_run }} fix --root "{{ justfile_directory() }}" {{ args }}

# Check that only the skill directories of the agents in Skills.Agents exist
[group('Agent Skills')]
skills-check *args:
    {{ _skills_run }} check --root "{{ justfile_directory() }}" {{ args }}

# Remove the skills CLI's links from agent directories Skills.Agents does not select
[group('Agent Skills')]
skills-fix *args:
    {{ _skills_run }} fix --root "{{ justfile_directory() }}" {{ args }}
