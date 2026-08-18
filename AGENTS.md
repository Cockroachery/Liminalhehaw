# Repository Instructions

## Required automatic Git wrap-up

For every Codex turn that changes any file in this repository, complete the following before sending the final response:

1. Run `git add -A` so every current change is staged, including pre-existing, unrelated, generated, deleted, and untracked changes that are not excluded by `.gitignore`.
2. Create one commit containing everything as-is. Use a concise commit message describing the turn. Do not selectively omit or revert changes.
3. Push the current branch to its configured upstream remote with `git push`.

Do not leave a successful change turn with a dirty worktree or unpushed commits. If there are no changes, do not create an empty commit. If committing or pushing is blocked by authentication, missing identity, merge conflicts, hooks, connectivity, or remote configuration, preserve the work and clearly ask the user for the minimum action needed to unblock it.

## Git LFS

Honor the repository's `.gitattributes` rules. Store matching binary assets through Git LFS, while keeping Unity YAML files and their `.meta` files in normal Git.
