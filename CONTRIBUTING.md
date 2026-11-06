# Branch Structure

## Public Branches

These branches are on the HardLight repo and should generally never directly merged or pushed into (neither on a fork or on the official repository)

- prod
  - This is the version the game server is running on
  - It should always be only comprised of tested and validated code
  - direct merges into prod are prohibited
  - Merges into prod happen weekly and are only comprised of code from master that has been individually tested by multiple people
  - Hotfix PRs into prod need to be reviewed for code quality _and_ tested by at least one maintainer, excluding the PR author. See [[#Testing Guidelines]]
- master
  - This is the branch the test server is running on (once available)
    - The test server is publicly available for whitelisted players
    - The test server will be updated and restarted daily (if changes exist)
  - PRs into master have to be tested by the developer for stability and functionality. See [[#Testing Guidelines]] and [[#PR Guidelines]]
  - features merged into master can be announced in upcoming-content on the discord

# PR Guidelines

- PRs need to come from one of the aforementioned forks branches. You cannot PR from your own master or prod branch into HardLight
- All content of a PR should be related to one topic.
- The title and body of a PR need to follow guidelines
  - The title needs to be prefixed with one of the following prefixes
    - **feat**: A new feature
    - **bugfix**: A bug fix
    - **hotfix**: A hot fix
    - **build**: Changes that affect the build system or external dependencies
    - **conf**: Changes to configurations
    - **docs**: Documentation only changes
    - **perf**: A code change that improves performance
    - **refactor**: A code change that neither fixes a bug nor adds a feature
    - **style**: Changes that do not affect the meaning of the code (white-space, formatting, missing semi-colons, etc)
    - **test**: Adding missing tests or correcting existing tests
    - **chore**: Updates and similar tasks, No production code change
  - The title should include the related issue number(s) (if applicable) and be a concise summary of the changes
  - The body should follow the standardized body with every applicable category filled out
- Refactors and cleanups must be in a separate PR
- A PR needs to be tested thoroughly by the developer before submission. See [[#Testing Guidelines]]
- A PR is not considered _submitted_ while it is in draft or is prefixed with `WIP: `
- Keep your PR as small and concise as possible. If you need to implement/port things for your main feature to work, PR these implementations/ports individually first

# Git Standards

- Update your PR via rebase instead of merge commits to keep the history clean.
- You should try to write commit messages that actually explain what you changed and why. Don't just say "fix stuff" or "wip".
- Every commit in your PR should be prefixed just like how the PR title is.
- You _may_ make commits that don't have proper messages, but it will have to be fixed by you or a maintainer before it's merged.
- Keep commits focused on one thing. If you've got a massive commit that's hard to review, we might ask you to split it up.
- For big features, break them into logical steps ("add new ahelp system", "move submitted ahelps to new system", "move entity log to use new ahelp system").
- It is a good idea to turn on "allow edits from maintainers" for your PRs. We can help clean up your git history.
- If you don't let us edit your PR and it needs git cleanup, you'll need to fix it yourself or give us the permission to help.
- If your git history is a complete mess that we can't reasonably fix, you'll need to clean it up yourself before we can merge it.
- The nicer your git history looks, the faster we can review and merge your PR.

# Testing Guidelines

- When building and starting the game, no new errors or warnings shall appear in either the server or client console
- Every aspect of your change should be tested
  - YML entity additions/changes
    - Every related entity needs to be spawned, used and observed for functionality
    - The entity needs to be locally saved and loaded on a ship at least once
    - The entity needs to be locally saved and loaded in an apartment at least once
    - child entities of the changed entity need to be tested as well
  - YML recipes
    - Every construction and deconstruction recipe has to be tested at least once
    - If the recipe has alternative construction materials, all combinations should be tested
  - YML game rules
    - The game rule has to be added 5 consecutive times
      - No warnings or errors shall appear in the consoles
  - C# Components
    - a wide variety of entities using the component need to be spawned, used and observed
    - an entity using the component needs to be saved and loaded on a ship
  - New/Ported C# systems
    - The needs are entirely based on the system itself
    - It is required to summarize what kinds of tests have been done
- Unit/Integration tests
  - C# code that adds functionality needs a unit test for every public functionality
